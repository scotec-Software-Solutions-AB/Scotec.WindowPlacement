namespace Scotec.WindowPlacement;

/// <summary>
///     Captures native Windows window placement, resolves persisted placements after monitor
///     topology or DPI changes, and optionally applies a placement to an HWND. All persistent geometry
///     is work-area-relative and measured in DIPs; resolved geometry is measured in physical pixels.
///     The service does not manage storage or automatically monitor display changes.
///     Implementations using Win32 APIs throw <see cref="System.PlatformNotSupportedException" />
///     on operating systems other than Windows.
/// </summary>
/// <remarks>
///     <para>
///         Typical flow: call <see cref="GetCurrentPlacement" /> when a window closes and persist the
///         result; later call <see cref="GetPlacement" /> to obtain a placement that is valid for the
///         monitors that exist now, and then apply it with <see cref="ApplyPlacement" /> or with a
///         framework adapter (WPF, Avalonia).
///     </para>
///     <para>
///         Methods that take an HWND must be called on the thread that owns the window.
///     </para>
/// </remarks>
public interface IWindowPlacementService
{
	/// <summary>
	///     Gets the normal outer rectangle, monitor, DPI and sizing state of an existing native
	///     top-level window. Maximized windows are captured using their normal/restored rectangle, and
	///     minimized windows are saved as normal. The HWND must be valid. The Win32 implementation temporarily switches the calling thread
	///     to a per-monitor-aware context for physical measurements, restoring it on return.
	/// </summary>
	/// <param name="hwnd">Existing Windows top-level window handle.</param>
	/// <returns>
	///     Serializable placement using monitor-work-area-relative DIPs. The monitor snapshot inside it
	///     is in physical pixels and identifies the monitor by interface path (if available) and name.
	/// </returns>
	SavedWindowPlacement GetCurrentPlacement(nint hwnd);

	/// <summary>
	///     Maps a saved placement to currently attached monitors. The original monitor is
	///     preferred by a unique interface path, then by a non-conflicting display name; otherwise the
	///     geometrically closest available monitor is used, with the primary monitor winning ties.
	///     Relative DIP coordinates are preserved when possible. Resizable windows are reduced to fit
	///     the working area, optionally respecting explicit minimum dimensions. Fixed-size windows keep
	///     their dimensions and are centered on the work area if too large. The input is never changed
	///     and no window is moved.
	/// </summary>
	/// <param name="savedPlacement">The persisted placement.</param>
	/// <param name="hwnd">
	///     Optional existing HWND used to inspect current native resizing style;
	///     zero means use the resizing information stored in the placement.
	/// </param>
	/// <param name="constraints">Optional application-provided minimum dimensions in DIPs.</param>
	/// <returns>
	///     Absolute physical rectangle on a currently available monitor (virtual-desktop pixels, which
	///     can be negative for monitors left of or above the primary monitor).
	/// </returns>
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
	/// <param name="placement">Resolved native placement in physical virtual-desktop pixels.</param>
	void ApplyPlacement(nint hwnd, ResolvedWindowPlacement placement);
}
