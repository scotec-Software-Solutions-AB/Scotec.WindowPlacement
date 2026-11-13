using System;
using System.Windows;
using System.Windows.Interop;

namespace Scotec.WindowPlacement.Wpf;

/// <summary>
///     Applies framework-independent placements to WPF windows.
///     Core results are absolute physical screen pixels (outer window rectangle). The adapter does
///     not convert them to WPF DIPs, because the window's current <c>HwndSource</c> DPI can differ
///     from the DPI of the monitor the placement resolves to (for example a dialog created on a 150%
///     monitor and restored to a 100% monitor). Instead the exact physical bounds are applied natively
///     through <see cref="Win32WindowPlacementService.ApplyPlacement" />. The caller should invoke it
///     after SourceInitialized and before the dialog is shown, on the window's UI thread.
/// </summary>
public static class WindowPlacementExtensions
{
    /// <summary>
    ///     Applies a resolved native outer rectangle to a WPF window by setting
    ///     <see cref="Window.WindowStartupLocation" /> and <see cref="Window.SizeToContent" /> to manual
    ///     and calling the native <c>ApplyPlacement</c> on the window handle.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The native apply restores the normal bounds with <c>SetWindowPos</c>, re-applies them once
    ///         if the window DPI changed during the move, and maximizes the window if requested. WPF
    ///         updates <c>Left</c>, <c>Top</c>, <c>Width</c> and <c>Height</c> in response to the native
    ///         move, so those properties can briefly lag behind the native bounds.
    ///     </para>
    ///     <para>
    ///         The native call is synchronous and can deliver window messages such as <c>WM_DPICHANGED</c>.
    ///         A 150% / 100% two-monitor setup was verified manually; this is not covered by automated tests.
    ///     </para>
    /// </remarks>
    /// <param name="placement">Resolved physical window rectangle.</param>
    /// <param name="window">Created WPF Window with an available native HWND.</param>
    /// <exception cref="PlatformNotSupportedException">The operating system is not Windows.</exception>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">The placement bounds are not positive.</exception>
    /// <exception cref="InvalidOperationException">The window handle does not exist yet (called before SourceInitialized).</exception>
    public static void ApplyTo(this ResolvedWindowPlacement placement, Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Wpf window placement adapter supports Windows only.");
        }

        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(window);
        // Only the native window handle is needed: the exact physical bounds are applied natively, so the
        // HwndSource/CompositionTarget DPI transform is not used. WindowInteropHelper.Handle is valid from
        // SourceInitialized on, whereas PresentationSource.FromVisual and CompositionTarget can still be
        // null at that point.
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            throw new InvalidOperationException("ApplyTo must be called after the window handle was created (SourceInitialized).");
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
        new Win32WindowPlacementService().ApplyPlacement(handle, placement);
    }
}
