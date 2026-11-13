using System;
using System.Collections.Generic;
using System.Linq;

namespace Scotec.WindowPlacement;

/// <summary>Native HWND placement service using short-lived per-monitor-aware thread DPI scopes
/// for physical-pixel geometry. The original thread DPI-awareness context is restored before
/// returning to its caller. Neither the process DPI awareness nor existing window contexts change.
/// Calls must be synchronous on the owning window's UI thread. Windows only.</summary>
public sealed class Win32WindowPlacementService : IWindowPlacementService
{
    private readonly IMonitorProvider _monitors;

    /// <summary>Creates a service using the system display enumeration provider.</summary>
    public Win32WindowPlacementService() : this(new Win32MonitorProvider()) { }

    /// <summary>Creates a placement service with an injectable monitor provider, useful for tests.</summary>
    /// <param name="monitors">Current display provider.</param>
    public Win32WindowPlacementService(IMonitorProvider monitors) =>
        _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));

    /// <inheritdoc />
    public SavedWindowPlacement GetCurrentPlacement(nint hwnd)
    {
        EnsureWindows();
        CheckWindow(hwnd);
        using var dpiScope = DpiAwarenessScope.Enter();
        var native = new NativeMethods.WindowPlacementNative
        {
            Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WindowPlacementNative>()
        };
        if (!NativeMethods.GetWindowPlacement(hwnd, ref native)) throw NativeMethods.Error();
        if (!NativeMethods.GetWindowRect(hwnd, out var currentRect)) throw NativeMethods.Error();
        var monitors = _monitors.GetMonitors();
        if (monitors.Count == 0) throw new InvalidOperationException("No monitors available.");
        // WINDOWPLACEMENT normal bounds are workspace coordinates for ordinary top-level windows.
        // Translate via the monitor-specific difference between work and full monitor origins.
        var normal = native.NormalPosition.ToPlacementRect();
        // For minimized/maximized windows use a monitor associated with the stored normal
        // rectangle rather than the current icon/fullscreen bounds, when possible.
        var ownerMonitor = FindNearest(monitors, currentRect.ToPlacementRect());
        var isToolWindow = (NativeMethods.GetExtendedStyle(hwnd) & NativeMethods.WsExToolWindow) != 0;
        var adjusted = isToolWindow ? normal : new PlacementRect(
            normal.X + ownerMonitor.WorkAreaPixels.X - ownerMonitor.BoundsPixels.X,
            normal.Y + ownerMonitor.WorkAreaPixels.Y - ownerMonitor.BoundsPixels.Y,
            normal.Width, normal.Height);
        // For non-maximized windows, GetWindowRect is the more direct current physical rectangle.
        var isMaximized = native.ShowCmd == NativeMethods.SwShowMaximized;
        if (native.ShowCmd == NativeMethods.SwShowNormal && currentRect.ToPlacementRect().IsValid)
            adjusted = currentRect.ToPlacementRect();
        var monitor = FindNearest(monitors, adjusted);
        // Re-evaluate workspace offset when restored window is associated with a different monitor.
        if (!isToolWindow && native.ShowCmd != NativeMethods.SwShowNormal && !ReferenceEquals(monitor, ownerMonitor))
        {
            adjusted = new PlacementRect(
                normal.X + monitor.WorkAreaPixels.X - monitor.BoundsPixels.X,
                normal.Y + monitor.WorkAreaPixels.Y - monitor.BoundsPixels.Y,
                normal.Width, normal.Height);
        }
        var work = monitor.WorkAreaPixels;
        return new SavedWindowPlacement
        {
            Monitor = monitor,
            BoundsDip = new PlacementRect(
                (adjusted.X - work.X) * 96d / monitor.DpiX,
                (adjusted.Y - work.Y) * 96d / monitor.DpiY,
                adjusted.Width * 96d / monitor.DpiX,
                adjusted.Height * 96d / monitor.DpiY),
            IsResizable = (NativeMethods.GetStyle(hwnd) & NativeMethods.WsThickFrame) != 0,
            IsMaximized = isMaximized,
            WindowDpi = NativeMethods.GetDpiForWindow(hwnd)
        };
    }

    /// <inheritdoc />
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
    public void ApplyPlacement(nint hwnd, ResolvedWindowPlacement placement)
    {
        EnsureWindows();
        CheckWindow(hwnd);
        ArgumentNullException.ThrowIfNull(placement);
        var bounds = placement.BoundsPixels;
        if (!bounds.IsValid) throw new ArgumentException("Invalid physical placement bounds.", nameof(placement));
        using var dpiScope = DpiAwarenessScope.Enter();
        // SetWindowPos and ShowWindow can synchronously deliver native window messages.
        // Do not invoke framework APIs from this scope; call from the owning UI thread.
        // Restore first to avoid moving a maximized window's full-screen visible rectangle.
        NativeMethods.ShowWindow(hwnd, NativeMethods.SwShowNormal);
        if (!NativeMethods.SetWindowPos(hwnd, 0,
            Round(bounds.X), Round(bounds.Y), Round(bounds.Width), Round(bounds.Height),
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder))
            throw NativeMethods.Error();
        if (placement.IsMaximized) NativeMethods.ShowWindow(hwnd, NativeMethods.SwShowMaximized);
    }

    /// <summary>Ensures the native HWND placement service is only invoked on Windows.
    /// The Windows-targeted assembly can otherwise be loaded on an unsupported operating system,
    /// where the required Win32 entry points are unavailable.</summary>
    /// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Scotec.WindowPlacement requires Windows and native Win32 window handles.");
    }

    /// <summary>Verifies that the handle references a currently existing native window.</summary>
    private static void CheckWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
            throw new ArgumentException("A valid HWND is required.", nameof(hwnd));
    }

    /// <summary>Inspects the native thick-frame style to determine whether resizing is supported.</summary>
    private static bool GetResizable(nint hwnd)
    {
        CheckWindow(hwnd);
        return (NativeMethods.GetStyle(hwnd) & NativeMethods.WsThickFrame) != 0;
    }

    /// <summary>Finds the monitor whose rectangle has the smallest distance to a window rectangle,
    /// prioritizing the monitor containing its center when possible.</summary>
    private static MonitorSnapshot FindNearest(IReadOnlyList<MonitorSnapshot> monitors, PlacementRect window)
    {
        var centerX = window.X + window.Width / 2;
        var centerY = window.Y + window.Height / 2;
        return monitors.OrderBy(m => DistanceToRectSquared(centerX, centerY, m.BoundsPixels))
            .ThenBy(m => m.IsPrimary ? 0 : 1).First();
    }

    /// <summary>Computes the squared distance between a physical point and the nearest point within
    /// a physical monitor rectangle (zero when the point is inside that rectangle).</summary>
    private static double DistanceToRectSquared(double x, double y, PlacementRect r)
    {
        var dx = Math.Max(0, Math.Max(r.X - x, x - (r.X + r.Width)));
        var dy = Math.Max(0, Math.Max(r.Y - y, y - (r.Y + r.Height)));
        return dx * dx + dy * dy;
    }

    /// <summary>Rounds to the nearest Win32 integer coordinate using midpoint rounding away from zero.</summary>
    private static int Round(double value) => checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
}
