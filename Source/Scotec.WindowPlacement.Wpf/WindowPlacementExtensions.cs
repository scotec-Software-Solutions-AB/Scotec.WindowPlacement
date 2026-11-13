using System;
using System.Windows;
using System.Windows.Interop;

namespace Scotec.WindowPlacement.Wpf;

/// <summary>
///     Applies framework-independent placements using WPF's public window properties.
///     WPF position and size are DIPs, whereas core results are physical screen pixels. The active
///     HwndSource transform is used to convert screen coordinates into WPF coordinates; the caller
///     should invoke this after SourceInitialized and before the dialog is shown. In mixed-DPI setups
///     WPF can re-evaluate layout after changing monitors, so verify the result in the host's DPI
///     awareness context. The native ApplyPlacement method is preferred when exact HWND bounds matter.
/// </summary>
public static class WindowPlacementExtensions
{
    /// <summary>
    ///     Applies a resolved native outer rectangle to WPF window Left, Top, Width and Height.
    ///     The HwndSource DPI transform is used for converting physical pixels to WPF DIPs; this is a
    ///     best-effort conversion in mixed-DPI environments where the current WPF source DPI and target
    ///     monitor DPI differ. Normal size is assigned before an optional maximization. A window's
    ///     non-client chrome may cause small differences from the requested physical outer bounds.
    /// </summary>
    /// <param name="placement">Resolved physical window rectangle.</param>
    /// <param name="window">Created WPF Window with an available native HWND.</param>
    public static void ApplyTo(this ResolvedWindowPlacement placement, Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Wpf window placement adapter supports Windows only.");
        }

        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(window);
        var source = PresentationSource.FromVisual(window) as HwndSource;
        if (source?.CompositionTarget is null)
        {
            throw new InvalidOperationException("ApplyTo must be called after WPF SourceInitialized.");
        }

        var bounds = placement.BoundsPixels;
        if (!bounds.IsValid)
        {
            throw new ArgumentException("Placement bounds must be positive.", nameof(placement));
        }

        // The window's current HwndSource DPI may differ from the target monitor's DPI (for example a
        // dialog created on a 150% monitor that is restored to a 100% monitor), so a DIP conversion with
        // the current transform would be wrong. The exact physical outer bounds are applied natively;
        // WPF then handles the resulting WM_DPICHANGED itself and keeps the pixel size.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.SizeToContent = SizeToContent.Manual;
        new Win32WindowPlacementService().ApplyPlacement(source.Handle, placement);
    }
}
