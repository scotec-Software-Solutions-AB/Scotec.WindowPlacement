using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Scotec.WindowPlacement;

namespace Scotec.WindowPlacement.Wpf;

/// <summary>Applies framework-independent placements using WPF's public window properties.
/// WPF position and size are DIPs, whereas core results are physical screen pixels. The active
/// HwndSource transform is used to convert screen coordinates into WPF coordinates; the caller
/// should invoke this after SourceInitialized and before the dialog is shown. In mixed-DPI setups
/// WPF can re-evaluate layout after changing monitors, so verify the result in the host's DPI
/// awareness context. The native ApplyPlacement method is preferred when exact HWND bounds matter.</summary>
public static class WindowPlacementExtensions
{
    /// <summary>Applies a resolved native outer rectangle to WPF window Left, Top, Width and Height.
    /// The HwndSource DPI transform is used for converting physical pixels to WPF DIPs; this is a
    /// best-effort conversion in mixed-DPI environments where the current WPF source DPI and target
    /// monitor DPI differ. Normal size is assigned before an optional maximization. A window's
    /// non-client chrome may cause small differences from the requested physical outer bounds.</summary>
    /// <param name="placement">Resolved physical window rectangle.</param>
    /// <param name="window">Created WPF Window with an available native HWND.</param>
    public static void ApplyTo(this ResolvedWindowPlacement placement, Window window)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Wpf window placement adapter supports Windows only.");
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(window);
        var source = PresentationSource.FromVisual(window) as HwndSource;
        if (source?.CompositionTarget is null)
            throw new InvalidOperationException("ApplyTo must be called after WPF SourceInitialized.");
        var bounds = placement.BoundsPixels;
        if (!bounds.IsValid) throw new ArgumentException("Placement bounds must be positive.", nameof(placement));
        Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
        var origin = fromDevice.Transform(new Point(bounds.X, bounds.Y));
        var bottomRight = fromDevice.Transform(new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height));
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = origin.X;
        window.Top = origin.Y;
        window.Width = bottomRight.X - origin.X;
        window.Height = bottomRight.Y - origin.Y;
        if (placement.IsMaximized)
            window.WindowState = WindowState.Maximized;
    }
}
