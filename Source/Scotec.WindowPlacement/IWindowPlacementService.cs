namespace Scotec.WindowPlacement;

/// <summary>
///     Captures native Windows window placement, resolves persisted placements after monitor
///     topology or DPI changes, and optionally applies a placement to an HWND. All persistent geometry
///     is work-area-relative and measured in DIPs; resolved geometry is measured in physical pixels.
///     The service does not manage storage or automatically monitor display changes.
///     Implementations using Win32 APIs throw <see cref="System.PlatformNotSupportedException" />
///     on operating systems other than Windows.
/// </summary>
public interface IWindowPlacementService
{
    /// <summary>
    ///     Gets the normal outer rectangle, monitor, DPI and sizing state of an existing native
    ///     top-level window. Maximized windows are captured using their normal/restored rectangle, and
    ///     minimized windows are saved as normal. The HWND must be valid. The Win32 implementation temporarily switches the
    ///     calling thread
    ///     to a per-monitor-aware context for physical measurements, restoring it on return.
    /// </summary>
    /// <param name="hwnd">Existing Windows top-level window handle.</param>
    /// <returns>Serializable placement using monitor-work-area-relative DIPs.</returns>
    SavedWindowPlacement GetCurrentPlacement(nint hwnd);

    /// <summary>
    ///     Maps a saved placement to currently attached monitors. The original monitor is
    ///     preferred by device name; otherwise the geometrically closest available monitor is used.
    ///     Relative DIP coordinates are preserved when possible. Resizable windows are reduced to fit
    ///     the working area, optionally respecting explicit minimum dimensions. Fixed-size windows keep
    ///     their dimensions and are centered on the work area if too large. The input is never changed.
    /// </summary>
    /// <param name="savedPlacement">The persisted placement.</param>
    /// <param name="hwnd">
    ///     Optional existing HWND used to inspect current native resizing style;
    ///     zero means use the resizing information stored in the placement.
    /// </param>
    /// <param name="constraints">Optional application-provided minimum dimensions in DIPs.</param>
    /// <returns>Absolute physical rectangle on a currently available monitor.</returns>
    ResolvedWindowPlacement GetPlacement(SavedWindowPlacement savedPlacement, nint hwnd = 0,
                                         WindowSizeConstraints? constraints = null);

    /// <summary>
    ///     Applies a resolved outer rectangle to an HWND using native window positioning and
    ///     optionally restores its maximized state. Caller must invoke it on the window's owning UI
    ///     thread. The implementation temporarily switches the thread context around native operations
    ///     and restores the original context even if positioning fails. Native window messages can be
    ///     dispatched synchronously by positioning APIs. Unlike framework adapters, this method
    ///     operates on native outer window bounds rather than framework client size.
    /// </summary>
    /// <param name="hwnd">Existing Windows top-level window handle.</param>
    /// <param name="placement">Resolved native placement.</param>
    void ApplyPlacement(nint hwnd, ResolvedWindowPlacement placement);
}
