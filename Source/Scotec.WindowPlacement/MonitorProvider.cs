using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Scotec.WindowPlacement;

/// <summary>Abstracts enumeration of connected displays so the resolver can be tested without Win32.</summary>
public interface IMonitorProvider
{
    /// <summary>Returns a current immutable snapshot of available monitor work areas and effective DPI.</summary>
    IReadOnlyList<MonitorSnapshot> GetMonitors();
}

/// <summary>
///     Enumerates displays in a temporary per-monitor DPI-aware thread context, yielding
///     physical desktop rectangles and effective DPI independently of the host process' DPI mode.
///     The previous calling thread context is restored before returning. Windows only.
/// </summary>
public sealed class Win32MonitorProvider : IMonitorProvider
{
    /// <inheritdoc />
    public IReadOnlyList<MonitorSnapshot> GetMonitors()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Win32 monitor enumeration is only available on Windows.");
        }

        using var dpiScope = DpiAwarenessScope.Enter();
        var monitors = new List<MonitorSnapshot>();
        NativeMethods.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new NativeMethods.MonitorInfoEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                Device = string.Empty
            };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                throw NativeMethods.Error();
            }

            var dpiX = 96u;
            var dpiY = 96u;
            // MDT_EFFECTIVE_DPI (0) is meaningful because the thread is per-monitor DPI-aware.
            // A failure is explicit: silently assuming 96 DPI would corrupt saved placement.
            var hr = NativeMethods.GetDpiForMonitor(monitor, 0, out var x, out var y);
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
                WorkAreaPixels = info.Work.ToPlacementRect(),
                DpiX = dpiX == 0 ? 96 : dpiX,
                DpiY = dpiY == 0 ? 96 : dpiY,
                IsPrimary = (info.Flags & NativeMethods.MonitorInfoPrimary) != 0
            });
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
    /// <returns>The monitor interface path when unambiguously available, otherwise null.</returns>
    private static string? GetMonitorDevicePath(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        string? path = null;
        for (uint i = 0; i < 32; i++)
        {
            var device = new NativeMethods.DisplayDevice
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.DisplayDevice>()
            };
            if (!NativeMethods.EnumDisplayDevices(displayName, i, ref device,
                    NativeMethods.EddGetDeviceInterfaceName))
            {
                break;
            }

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
