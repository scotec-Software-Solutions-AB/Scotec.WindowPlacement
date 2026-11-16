using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Scotec.WindowPlacement.Avalonia;

/// <summary>
///     Provides an Avalonia-specific adapter for applying a framework-independent resolved
///     Win32 window placement to an <see cref="Window" />. Window positions in Avalonia use
///     <see cref="PixelPoint" /> (screen pixels), whereas window Width and Height use
///     device-independent pixels (DIPs). The adapter is intended for Windows desktop windows.
///     It does not depend on Avalonia's internal Win32 implementation.
/// </summary>
public static class WindowPlacementExtensions
{
    /// <summary>
    ///     Applies a resolved placement to an Avalonia window using public Avalonia properties and
    ///     the core native <c>ApplyPlacement</c>. The resolved position is an absolute physical screen
    ///     coordinate and is assigned to <see cref="Window.Position" /> without DPI scaling. The physical
    ///     outer width and height are converted to an initial DIP estimate with the destination monitor's
    ///     effective DPI, independently for each axis, and then replaced by the exact native bounds
    ///     (see remarks). WindowState is restored to Normal first; the maximized state is restored by
    ///     the native apply.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Call this on the Avalonia UI thread, preferably before the window becomes visible.
    ///         WindowStartupLocation is set to Manual so automatic centering does not replace the position;
    ///         SizeToContent is set to Manual so layout does not override the restored size.
    ///     </para>
    ///     <para>
    ///         The resolved bounds are the native OUTER rectangle (title bar and borders included), whereas
    ///         Avalonia Width and Height are CLIENT sizes. The adapter therefore works in three steps:
    ///         (1) Position, Width and Height are set as an initial estimate (the size is slightly too
    ///         large); (2) the exact outer bounds are applied natively with the core ApplyPlacement, either
    ///         immediately if the window is visible, or once after the Opened event, posted at
    ///         <c>DispatcherPriority.Loaded</c> so it runs after Avalonia's initial layout pass (Avalonia
    ///         re-applies Width and Height while showing the window); (3) Width and Height are pinned to the
    ///         resulting ClientSize, otherwise a later layout pass would use the outer size as client size
    ///         and the window would grow by its frame size on every restore.
    ///     </para>
    ///     <para>
    ///         A brief flash at the estimated size before the window has opened is possible. A 150% / 100%
    ///         two-monitor setup was verified manually; other DPI combinations and Revit hosting are not
    ///         covered by automated tests.
    ///     </para>
    /// </remarks>
    /// <param name="placement">
    ///     The resolved placement, containing absolute physical
    ///     screen bounds and the selected monitor's DPI.
    /// </param>
    /// <param name="window">The target Avalonia window.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Bounds or monitor DPI are invalid or the
    ///     pixel position is outside the range supported by <see cref="PixelPoint" />.
    /// </exception>
    public static void ApplyTo(this ResolvedWindowPlacement placement, Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Avalonia window placement adapter supports Windows only.");
        }

        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(window);

        var bounds = placement.BoundsPixels;
        if (!bounds.IsValid)
        {
            throw new ArgumentException("Resolved placement bounds must be finite and positive.", nameof(placement));
        }

        double dpiX = placement.Monitor.DpiX;
        double dpiY = placement.Monitor.DpiY;
        if (dpiX <= 0 || dpiY <= 0)
        {
            throw new ArgumentException("The destination monitor DPI must be positive.", nameof(placement));
        }

        var x = ToPixelCoordinate(bounds.X, nameof(bounds.X));
        var y = ToPixelCoordinate(bounds.Y, nameof(bounds.Y));

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.SizeToContent = SizeToContent.Manual;

        // Always set Avalonia's own Position/Width/Height: Avalonia re-applies these properties when the
        // window is shown, so applying only native bounds would be overwritten by the XAML size. The
        // logical size uses the destination monitor DPI, not the DPI of the monitor the window lives on.
        window.WindowState = WindowState.Normal;
        window.Position = new PixelPoint(x, y);
        // NOTE: the resolved bounds are the native OUTER rectangle (title bar and borders included) while
        // Avalonia Width/Height are CLIENT sizes. Using the outer size as client size is therefore only a
        // first estimate for the initial layout; the exact outer bounds are applied natively afterwards.
        window.Width = bounds.Width * 96.0 / dpiX;
        window.Height = bounds.Height * 96.0 / dpiY;

        if (window.IsVisible)
        {
            ApplyNative(window, placement);
            return;
        }

        // Avalonia applies Width/Height again while showing the window, so the exact native bounds are
        // applied once after it opened, posted behind the initial layout pass. The handler unsubscribes itself.
        void OnOpened(object? sender, EventArgs e)
        {
            window.Opened -= OnOpened;
            Dispatcher.UIThread.Post(() => ApplyNative(window, placement), DispatcherPriority.Loaded);
        }

        window.Opened += OnOpened;
    }

    /// <summary>
    ///     Applies the exact physical outer bounds natively, then pins Avalonia's Width/Height to the
    ///     resulting client size so a later layout pass cannot re-apply the larger outer size as client size
    ///     (which made the window grow on every restore).
    /// </summary>
    /// <param name="window">Avalonia window.</param>
    /// <param name="placement">Resolved placement in physical pixels.</param>
    private static void ApplyNative(Window window, ResolvedWindowPlacement placement)
    {
        if (window.TryGetPlatformHandle() is { } handle && handle.Handle != 0)
        {
            new Win32WindowPlacementService().ApplyPlacement(handle.Handle, placement);
            window.Width = window.ClientSize.Width;
            window.Height = window.ClientSize.Height;
        }
        else if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    ///     Rounds a physical screen coordinate to the nearest integer pixel, using away-from-zero
    ///     midpoint rounding, and ensures that it is representable by Avalonia's PixelPoint.
    /// </summary>
    /// <param name="value">
    ///     Physical screen coordinate, which may be negative for monitors
    ///     situated to the left of or above the primary monitor.
    /// </param>
    /// <param name="parameterName">Name used to identify the invalid coordinate in an exception.</param>
    /// <returns>The rounded pixel coordinate.</returns>
    /// <exception cref="ArgumentException">The coordinate is not finite or cannot fit in Int32.</exception>
    private static int ToPixelCoordinate(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
        {
            throw new ArgumentException($"Invalid physical screen coordinate: {parameterName}.", parameterName);
        }

        return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
    }
}
