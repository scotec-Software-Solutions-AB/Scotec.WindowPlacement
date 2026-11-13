using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Scotec.WindowPlacement;

/// <summary>
///     Native HWND placement service using short-lived per-monitor-aware thread DPI scopes
///     for physical-pixel geometry. The original thread DPI-awareness context is restored before
///     returning to its caller. Neither the process DPI awareness nor existing window contexts change.
///     Calls must be synchronous on the owning window's UI thread. Windows only.
/// </summary>
/// <remarks>
///     <para>
///         This class is the only place that combines native window queries with the pure
///         <see cref="WindowPlacementResolver" />. Capture (<see cref="GetCurrentPlacement" />) converts
///         physical pixels to monitor-relative DIPs; resolution (<see cref="GetPlacement" />) does the
///         reverse for the monitors that exist now; <see cref="ApplyPlacement" /> moves the window.
///     </para>
///     <para>
///         Coordinate systems involved: <c>GetWindowRect</c> returns physical virtual-desktop
///         coordinates (in the per-monitor DPI scope); <c>WINDOWPLACEMENT.rcNormalPosition</c> is in
///         workspace coordinates (relative to the work area) for ordinary windows; monitor rectangles
///         and work areas are physical virtual-desktop coordinates; saved bounds are DIPs relative to
///         the monitor work-area origin.
///     </para>
/// </remarks>
public sealed class Win32WindowPlacementService : IWindowPlacementService
{
	/// <summary>The reference DPI (100 % scaling) at which one DIP equals one physical pixel.</summary>
	private const double ReferenceDpi = 96d;

	/// <summary>Source of the current monitor topology.</summary>
	private readonly IMonitorProvider _monitors;

	/// <summary>Creates a service using the system display enumeration provider.</summary>
	public Win32WindowPlacementService() : this(new Win32MonitorProvider())
	{
	}

	/// <summary>Creates a placement service with an injectable monitor provider, useful for tests.</summary>
	/// <param name="monitors">Current display provider.</param>
	/// <exception cref="ArgumentNullException"><paramref name="monitors" /> is null.</exception>
	public Win32WindowPlacementService(IMonitorProvider monitors)
	{
		_monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
	}

	/// <inheritdoc />
	/// <remarks>
	///     The normal rectangle is chosen by show state: for a window in the normal state the
	///     current <c>GetWindowRect</c> is used directly (it is already in physical screen
	///     coordinates); for a maximized or minimized window the restored rectangle from
	///     <c>GetWindowPlacement</c> is used and translated from workspace to screen coordinates. The
	///     rectangle is the outer window rectangle and can include invisible resize borders on
	///     DWM-composed windows.
	/// </remarks>
	/// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
	/// <exception cref="ArgumentException"><paramref name="hwnd" /> is zero or not an existing window.</exception>
	/// <exception cref="InvalidOperationException">No monitors are available.</exception>
	/// <exception cref="System.ComponentModel.Win32Exception">A native query failed.</exception>
	public SavedWindowPlacement GetCurrentPlacement(nint hwnd)
	{
		EnsureWindows();
		CheckWindow(hwnd);
		// Everything below must see physical pixels. No framework or UI code runs inside this scope.
		using var dpiScope = DpiAwarenessScope.Enter();
		var native = new NativeMethods.WindowPlacementNative
		{
			// The structure's size must be provided to GetWindowPlacement.
			Length = (uint)Marshal.SizeOf<NativeMethods.WindowPlacementNative>()
		};
		if (!NativeMethods.GetWindowPlacement(hwnd, ref native))
		{
			throw NativeMethods.Error();
		}

		if (!NativeMethods.GetWindowRect(hwnd, out var currentRect))
		{
			throw NativeMethods.Error();
		}

		var monitors = _monitors.GetMonitors();
		if (monitors.Count == 0)
		{
			throw new InvalidOperationException("No monitors available.");
		}

		// WINDOWPLACEMENT normal bounds are workspace coordinates for ordinary top-level windows.
		// Translate via the monitor-specific difference between work and full monitor origins.
		var normal = native.NormalPosition.ToPlacementRect();
		// The monitor that currently hosts the window. For a maximized or minimized window the
		// current rectangle lies on the monitor where it was maximized or minimized, which is also the
		// monitor to which the workspace-relative normal rectangle belongs.
		var ownerMonitor = FindNearest(monitors, currentRect.ToPlacementRect());
		// WS_EX_TOOLWINDOW windows report the normal rectangle in screen coordinates already.
		var isToolWindow = (NativeMethods.GetExtendedStyle(hwnd) & NativeMethods.WsExToolWindow) != 0;
		var adjusted = isToolWindow
			? normal
			: new PlacementRect(
				normal.X + ownerMonitor.WorkAreaPixels.X - ownerMonitor.BoundsPixels.X,
				normal.Y + ownerMonitor.WorkAreaPixels.Y - ownerMonitor.BoundsPixels.Y,
				normal.Width, normal.Height);
		// For non-maximized windows, GetWindowRect is the more direct current physical rectangle.
		var isMaximized = native.ShowCmd == NativeMethods.SwShowMaximized;
		if (native.ShowCmd == NativeMethods.SwShowNormal && currentRect.ToPlacementRect().IsValid)
		{
			adjusted = currentRect.ToPlacementRect();
		}

		// The monitor that the normal rectangle belongs to; it defines the saved work-area origin and DPI.
		var monitor = FindNearest(monitors, adjusted);
		// Re-evaluate workspace offset when restored window is associated with a different monitor.
		if (!isToolWindow && native.ShowCmd != NativeMethods.SwShowNormal && !ReferenceEquals(monitor, ownerMonitor))
		{
			adjusted = new PlacementRect(
				normal.X + monitor.WorkAreaPixels.X - monitor.BoundsPixels.X,
				normal.Y + monitor.WorkAreaPixels.Y - monitor.BoundsPixels.Y,
				normal.Width, normal.Height);
		}

		// Physical pixels -> DIPs relative to the monitor's work-area origin, using the monitor's own
		// effective DPI per axis (not the window DPI, which can be virtualized).
		var work = monitor.WorkAreaPixels;
		return new SavedWindowPlacement
		{
			Monitor = monitor,
			BoundsDip = new PlacementRect(
				(adjusted.X - work.X) * ReferenceDpi / monitor.DpiX,
				(adjusted.Y - work.Y) * ReferenceDpi / monitor.DpiY,
				adjusted.Width * ReferenceDpi / monitor.DpiX,
				adjusted.Height * ReferenceDpi / monitor.DpiY),
			IsResizable = (NativeMethods.GetStyle(hwnd) & NativeMethods.WsThickFrame) != 0,
			IsMaximized = isMaximized,
			WindowDpi = NativeMethods.GetDpiForWindow(hwnd)
		};
	}

	/// <inheritdoc />
	/// <remarks>
	///     This method never moves a window and never changes <paramref name="savedPlacement" />.
	///     The monitor enumeration enters its own short DPI scope; the resizability style query does
	///     not depend on DPI, and the resolution itself is pure.
	/// </remarks>
	/// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="savedPlacement" /> is null.</exception>
	/// <exception cref="ArgumentException">
	///     <paramref name="hwnd" /> is non-zero but not an existing window, or the saved placement is invalid.
	/// </exception>
	public ResolvedWindowPlacement GetPlacement(SavedWindowPlacement savedPlacement,
												nint hwnd = 0, WindowSizeConstraints? constraints = null)
	{
		EnsureWindows();
		ArgumentNullException.ThrowIfNull(savedPlacement);
		// MonitorProvider enters its own temporary DPI scope. The style query does not depend
		// on DPI, and pure placement resolution does not call Win32 at all.
		var isResizable = hwnd == 0 ? savedPlacement.IsResizable : GetResizable(hwnd);
		return WindowPlacementResolver.Resolve(savedPlacement, _monitors.GetMonitors(), isResizable, constraints);
	}

	/// <inheritdoc />
	/// <remarks>
	///     The window is first restored so that a maximized window's full-screen rectangle is not
	///     what gets moved; the normal bounds are then applied, and the window is maximized again if
	///     <see cref="ResolvedWindowPlacement.IsMaximized" /> is set. The bounds are rounded to whole
	///     pixels. Windows can adjust the final size (minimum or maximum tracking size, WM_GETMINMAXINFO).
	/// </remarks>
	/// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
	/// <exception cref="ArgumentException">
	///     <paramref name="hwnd" /> is zero or not an existing window, or the bounds are invalid.
	/// </exception>
	/// <exception cref="ArgumentNullException"><paramref name="placement" /> is null.</exception>
	/// <exception cref="System.ComponentModel.Win32Exception">Positioning failed.</exception>
	/// <exception cref="OverflowException">A coordinate does not fit into a 32-bit integer.</exception>
	public void ApplyPlacement(nint hwnd, ResolvedWindowPlacement placement)
	{
		EnsureWindows();
		CheckWindow(hwnd);
		ArgumentNullException.ThrowIfNull(placement);
		var bounds = placement.BoundsPixels;
		if (!bounds.IsValid)
		{
			throw new ArgumentException("Invalid physical placement bounds.", nameof(placement));
		}

		using var dpiScope = DpiAwarenessScope.Enter();
		// SetWindowPos and ShowWindow can synchronously deliver native window messages.
		// Do not invoke framework APIs from this scope; call from the owning UI thread.
		// Restore first to avoid moving a maximized window's full-screen visible rectangle.
		// A hidden window (for example during WPF SourceInitialized) must stay hidden: ShowWindow would make
		// it visible before the framework has rendered its content, which is seen as black content for a
		// moment. SetWindowPos with SWP_NOACTIVATE does not change the visibility.
		var wasVisible = NativeMethods.IsWindowVisible(hwnd);
		if (wasVisible)
		{
			NativeMethods.ShowWindow(hwnd, NativeMethods.SwShowNormal);
		}
		// Moving a window to a monitor with a different DPI makes Windows send WM_DPICHANGED during
		// SetWindowPos; per-monitor-aware frameworks then rescale the window from its old DPI, which
		// can overwrite the size just set. Therefore the physical bounds are applied again once if the
		// window DPI changed. One retry is enough because the DPI is stable after the first move.
		var dpiBefore = NativeMethods.GetDpiForWindow(hwnd);
		SetBounds(hwnd, bounds);
		if (NativeMethods.GetDpiForWindow(hwnd) != dpiBefore)
		{
			SetBounds(hwnd, bounds);
		}

		if (placement.IsMaximized)
		{
			NativeMethods.ShowWindow(hwnd, NativeMethods.SwShowMaximized);
		}
	}

	/// <summary>
	///     Sets the outer window rectangle in physical desktop pixels without changing activation or Z order.
	/// </summary>
	/// <param name="hwnd">Existing window handle.</param>
	/// <param name="bounds">Valid physical outer bounds.</param>
	/// <exception cref="System.ComponentModel.Win32Exception">SetWindowPos failed.</exception>
	private static void SetBounds(nint hwnd, PlacementRect bounds)
	{
		if (!NativeMethods.SetWindowPos(hwnd, 0,
				Round(bounds.X), Round(bounds.Y), Round(bounds.Width), Round(bounds.Height),
				NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder))
		{
			throw NativeMethods.Error();
		}
	}

	/// <summary>
	///     Ensures the native HWND placement service is only invoked on Windows.
	///     The Windows-targeted assembly can otherwise be loaded on an unsupported operating system,
	///     where the required Win32 entry points are unavailable.
	/// </summary>
	/// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
	private static void EnsureWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("Scotec.WindowPlacement requires Windows and native Win32 window handles.");
		}
	}

	/// <summary>Verifies that the handle references a currently existing native window.</summary>
	/// <param name="hwnd">The window handle to check.</param>
	/// <exception cref="ArgumentException">The handle is zero or no longer identifies a window.</exception>
	private static void CheckWindow(nint hwnd)
	{
		if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
		{
			throw new ArgumentException("A valid HWND is required.", nameof(hwnd));
		}
	}

	/// <summary>
	///     Inspects the native thick-frame style to determine whether resizing is supported.
	///     This is a heuristic: it does not detect application-specific size limits.
	/// </summary>
	/// <param name="hwnd">An existing window handle.</param>
	/// <returns>True if the window has a sizing border (WS_THICKFRAME).</returns>
	/// <exception cref="ArgumentException">The handle is zero or not an existing window.</exception>
	private static bool GetResizable(nint hwnd)
	{
		CheckWindow(hwnd);
		return (NativeMethods.GetStyle(hwnd) & NativeMethods.WsThickFrame) != 0;
	}

	/// <summary>
	///     Finds the monitor whose rectangle has the smallest distance to the center of a window
	///     rectangle. A monitor that contains the center has distance zero, so it wins; if the
	///     center is outside every monitor, the nearest one is used. The primary monitor wins ties.
	/// </summary>
	/// <param name="monitors">Non-empty list of current monitors in physical pixels.</param>
	/// <param name="window">Window rectangle in the same virtual-desktop pixel coordinates.</param>
	/// <returns>The monitor that best represents the window's location.</returns>
	private static MonitorSnapshot FindNearest(IReadOnlyList<MonitorSnapshot> monitors, PlacementRect window)
	{
		var centerX = window.X + window.Width / 2;
		var centerY = window.Y + window.Height / 2;
		return monitors.OrderBy(m => DistanceToRectSquared(centerX, centerY, m.BoundsPixels))
					   .ThenBy(m => m.IsPrimary ? 0 : 1).First();
	}

	/// <summary>
	///     Computes the squared distance between a physical point and the nearest point within
	///     a physical monitor rectangle (zero when the point is inside that rectangle).
	/// </summary>
	/// <param name="x">Horizontal point coordinate in virtual-desktop pixels.</param>
	/// <param name="y">Vertical point coordinate in virtual-desktop pixels.</param>
	/// <param name="r">Monitor rectangle in the same coordinate system.</param>
	/// <returns>The squared distance in pixels.</returns>
	private static double DistanceToRectSquared(double x, double y, PlacementRect r)
	{
		// Per axis, the distance is zero while the point lies between the rectangle's edges.
		var dx = Math.Max(0, Math.Max(r.X - x, x - (r.X + r.Width)));
		var dy = Math.Max(0, Math.Max(r.Y - y, y - (r.Y + r.Height)));
		return dx * dx + dy * dy;
	}

	/// <summary>Rounds to the nearest Win32 integer coordinate using midpoint rounding away from zero.</summary>
	/// <param name="value">A physical pixel coordinate or size.</param>
	/// <returns>The rounded integer value.</returns>
	/// <exception cref="OverflowException">The value does not fit into a 32-bit integer.</exception>
	private static int Round(double value)
	{
		return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
	}
}
