using System;
using Avalonia;
using Avalonia.Controls;

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
    ///     Applies a resolved placement to an Avalonia window using public Avalonia properties.
    ///     The resolved position is an absolute physical screen coordinate and is therefore assigned
    ///     to <see cref="Window.Position" /> without DPI scaling. Physical width and height are
    ///     converted to DIPs with the destination monitor's effective DPI, independently for each
    ///     axis. WindowState is restored to Normal first, then Maximized if requested.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Call this on the Avalonia UI thread once the window has a platform handle, preferably
    ///         before the window becomes visible. Setting WindowStartupLocation to Manual prevents
    ///         automatic centering from intentionally replacing the position. SizeToContent is set to
    ///         Manual so layout does not intentionally override the restored size.
    ///     </para>
    ///     <para>
    ///         Framework Width and Height may describe the logical window content dimensions rather
    ///         than the exact native outer rectangle, depending on window decorations and platform
    ///         implementation. For pixel-exact outer frame placement, use the core HWND ApplyPlacement
    ///         operation instead. DPI changes during showing or after moving to another monitor may
    ///         trigger additional platform layout; mixed-DPI and Revit-hosted cases need testing.
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

        // When the native window already exists, apply the exact physical outer bounds natively so that
        // the DPI change of crossing monitors is handled once by the platform and not mis-scaled here.
        if (window.TryGetPlatformHandle() is { } handle && handle.Handle != 0)
        {
            new Win32WindowPlacementService().ApplyPlacement(handle.Handle, placement);
            return;
        }

        // No native window yet: use logical size from the destination monitor DPI.
        window.WindowState = WindowState.Normal;
        window.Position = new PixelPoint(x, y);
        window.Width = bounds.Width * 96.0 / dpiX;
        window.Height = bounds.Height * 96.0 / dpiY;

        if (placement.IsMaximized)
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
