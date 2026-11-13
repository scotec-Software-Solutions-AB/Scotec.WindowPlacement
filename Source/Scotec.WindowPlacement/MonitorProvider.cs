using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Scotec.WindowPlacement;

/// <summary>
///     Abstracts the enumeration of connected displays, so that placement logic can be tested without
///     Win32 by supplying synthetic monitor topologies.
/// </summary>
public interface IMonitorProvider
{
	/// <summary>
	///     Returns a snapshot of the currently attached monitors. All rectangles are in physical
	///     virtual-desktop pixels (the origin is the primary monitor's top-left, so coordinates can be
	///     negative) and the DPI values are the monitors' effective DPI.
	/// </summary>
	/// <returns>
	///     An immutable list of monitors. Implementations should not return an empty list on a working
	///     system; callers treat an empty list as an error.
	/// </returns>
	IReadOnlyList<MonitorSnapshot> GetMonitors();
}

/// <summary>
///     Enumerates displays in a temporary per-monitor DPI-aware thread context, yielding
///     physical desktop rectangles and effective DPI independently of the host process' DPI mode.
///     The previous calling thread context is restored before returning. Windows only.
/// </summary>
/// <remarks>
///     The class is stateless and thread-safe; each call queries the system again, so the result
///     always reflects the current topology (it is not cached). Call it from a thread that may
///     temporarily change its DPI context, and do not run UI code while it executes.
/// </remarks>
public sealed class Win32MonitorProvider : IMonitorProvider
{
	/// <summary>Maximum number of display targets examined per GDI display name when looking for the monitor interface path.</summary>
	private const uint MaxDisplayTargetsPerAdapter = 32;

	/// <summary>The reference DPI (100 % scaling), used when Windows reports a DPI of zero.</summary>
	private const uint ReferenceDpi = 96;

	/// <summary>MDT_EFFECTIVE_DPI: the DPI that Windows actually uses to scale content on a monitor.</summary>
	private const int EffectiveDpi = 0;

	/// <inheritdoc />
	/// <exception cref="PlatformNotSupportedException">The operating system is not Windows, or per-monitor DPI awareness is unavailable.</exception>
	/// <exception cref="System.ComponentModel.Win32Exception">A native call failed, including failure to enter or leave the DPI scope.</exception>
	/// <exception cref="COMException">The monitor DPI could not be determined (a failing HRESULT is surfaced, never replaced by 96 DPI).</exception>
	public IReadOnlyList<MonitorSnapshot> GetMonitors()
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("Win32 monitor enumeration is only available on Windows.");
		}

		// Per-monitor awareness is required so that GetMonitorInfo and GetDpiForMonitor return real
		// physical values instead of values virtualized for a DPI-unaware or system-aware host.
		using var dpiScope = DpiAwarenessScope.Enter();
		var monitors = new List<MonitorSnapshot>();

		// The callback runs synchronously on this thread, inside the DPI scope.
		NativeMethods.MonitorEnumProc callback = (monitor, _, _, _) =>
		{
			var info = new NativeMethods.MonitorInfoEx
			{
				// cbSize must be set, otherwise GetMonitorInfo fails.
				Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
				Device = string.Empty
			};
			if (!NativeMethods.GetMonitorInfo(monitor, ref info))
			{
				throw NativeMethods.Error();
			}

			var dpiX = ReferenceDpi;
			var dpiY = ReferenceDpi;
			// MDT_EFFECTIVE_DPI (0) is meaningful because the thread is per-monitor DPI-aware.
			// A failure is explicit: silently assuming 96 DPI would corrupt saved placement.
			var hr = NativeMethods.GetDpiForMonitor(monitor, EffectiveDpi, out var x, out var y);
			if (hr < 0)
			{
				Marshal.ThrowExceptionForHR(hr);
			}

			dpiX = x;
			dpiY = y;
			monitors.Add(new MonitorSnapshot
			{
				DeviceName = info.Device ?? string.Empty,
				MonitorDevicePath = GetMonitorDevicePath(info.Device),
				BoundsPixels = info.Monitor.ToPlacementRect(),
				// The work area excludes taskbars and docked toolbars; it is the region used for clamping.
				WorkAreaPixels = info.Work.ToPlacementRect(),
				// A reported DPI of zero is not usable as a scale factor; treat it as 100 %.
				DpiX = dpiX == 0 ? ReferenceDpi : dpiX,
				DpiY = dpiY == 0 ? ReferenceDpi : dpiY,
				IsPrimary = (info.Flags & NativeMethods.MonitorInfoPrimary) != 0
			});
			// Returning true continues the enumeration.
			return true;
		};
		if (!NativeMethods.EnumDisplayMonitors(0, 0, callback, 0))
		{
			throw NativeMethods.Error();
		}

		return monitors;
	}

	/// <summary>
	///     Looks up the GUID_DEVINTERFACE_MONITOR path associated with the given GDI
	///     display name. Returns null when the display driver does not expose an active monitor
	///     interface (common in some VM/RDP configurations). This lookup does not change state.
	/// </summary>
	/// <param name="displayName">GDI display name from GetMonitorInfo, such as DISPLAY2.</param>
	/// <returns>
	///     The monitor interface path when exactly one distinct active path exists for the display,
	///     otherwise null. The path identifies the attached monitor more stably than the GDI name,
	///     but it is still not guaranteed to survive driver, docking or remote-session changes.
	/// </returns>
	private static string? GetMonitorDevicePath(string? displayName)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return null;
		}

		string? path = null;
		// A display adapter name can have several attached monitors (targets); walk them all.
		// The index is bounded so that a misbehaving driver cannot cause an endless loop.
		for (uint i = 0; i < MaxDisplayTargetsPerAdapter; i++)
		{
			var device = new NativeMethods.DisplayDevice
			{
				Size = (uint)Marshal.SizeOf<NativeMethods.DisplayDevice>()
			};
			// A false result is the regular end of the enumeration, not an error.
			if (!NativeMethods.EnumDisplayDevices(displayName, i, ref device,
					NativeMethods.EddGetDeviceInterfaceName))
			{
				break;
			}

			// Inactive targets and entries without an interface path carry no usable identity.
			if ((device.StateFlags & NativeMethods.DisplayDeviceActive) == 0 ||
				string.IsNullOrWhiteSpace(device.DeviceId))
			{
				continue;
			}

			// Ambiguous active targets (e.g. unusual clone/driver configurations) must
			// not be assigned an arbitrary hardware identity. Use geometric fallback.
			if (path is not null && !string.Equals(path, device.DeviceId, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			path = device.DeviceId;
		}

		return path;
	}
}
