using System;
using System.Collections.Generic;
using System.Linq;

namespace Scotec.WindowPlacement;

/// <summary>
///     Pure-geometry implementation of monitor matching, monitor-relative DIP conversion and
///     visibility rules. This class does not access HWNDs or any native API and can be unit-tested
///     using synthetic monitor topologies.
/// </summary>
/// <remarks>
///     <para>
///         <b>Coordinate systems.</b> A saved placement stores its bounds in DIPs (96 per inch)
///         relative to the origin of the saved monitor's work area. The resolved result is in absolute
///         physical virtual-desktop pixels. Conversion uses the <i>target</i> monitor's effective DPI,
///         separately for each axis. Virtual-desktop origins may be negative.
///     </para>
///     <para>
///         <b>Determinism.</b> The result depends only on the arguments. Ties are resolved in favor of
///         the primary monitor, and then by enumeration order.
///     </para>
/// </remarks>
public static class WindowPlacementResolver
{
	/// <summary>The reference DPI at which one DIP equals one physical pixel.</summary>
	private const double ReferenceDpi = 96d;

	/// <summary>
	///     Resolves a persisted placement to physical screen coordinates on a current monitor.
	///     Prefers a unique device-interface path match, then a non-conflicting GDI display-name
	///     match, then the closest display rectangle with the primary display breaking ties.
	///     Does not modify the persisted input.
	/// </summary>
	/// <param name="saved">Original saved placement; bounds are DIPs relative to the saved monitor's work area.</param>
	/// <param name="available">Current monitors in physical pixels; must contain at least one entry.</param>
	/// <param name="isResizable">
	///     Whether the window can be resized. Resizable windows are shrunk to fit the work area;
	///     fixed-size windows keep their size.
	/// </param>
	/// <param name="constraints">Optional minimum dimensions in DIPs, applied only when resizing.</param>
	/// <returns>
	///     The chosen monitor and the normal window bounds in absolute physical virtual-desktop
	///     pixels. <see cref="ResolvedWindowPlacement.UsedFallbackMonitor" /> is true if the saved
	///     monitor could not be identified.
	/// </returns>
	/// <remarks>
	///     <para>
	///         <b>Algorithm.</b> (1) Select the target monitor. (2) Convert the saved DIP rectangle to
	///         pixels with the target DPI and add the target work-area origin, which preserves the
	///         logical offset when a different monitor is used. (3) Shrink resizable windows to the work area
	///         but not below the minimum. (4) Center axes that still do not fit (fixed or oversized
	///         minimum). (5) Clamp windows that do fit fully into the work area.
	///     </para>
	///     <para>
	///         <b>Known limitation.</b> A fixed-size window larger than the work area, or a resizable
	///         window whose minimum is larger than the work area, stays partly off-screen. This is
	///         unavoidable without distorting the content.
	///     </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="saved" /> or <paramref name="available" /> is null.</exception>
	/// <exception cref="ArgumentException">
	///     <paramref name="available" /> is empty, the saved bounds or saved work area are invalid, or
	///     the selected monitor has an invalid work area or a zero DPI.
	/// </exception>
	public static ResolvedWindowPlacement Resolve(SavedWindowPlacement saved,
												  IReadOnlyList<MonitorSnapshot> available, bool isResizable, WindowSizeConstraints? constraints = null)
	{
		ArgumentNullException.ThrowIfNull(saved);
		ArgumentNullException.ThrowIfNull(available);
		if (available.Count == 0)
		{
			throw new ArgumentException("No active displays.", nameof(available));
		}

		if (!saved.BoundsDip.IsValid || !saved.Monitor.WorkAreaPixels.IsValid)
		{
			throw new ArgumentException("Invalid saved window placement.", nameof(saved));
		}

		// Step 1: pick the target monitor. An identity match wins; otherwise take the monitor whose
		// rectangle is closest to the saved monitor's rectangle (zero for overlapping or touching
		// rectangles), preferring the primary monitor on equal distance.
		var matched = MatchMonitor(saved.Monitor, available);
		var target = matched ?? available.OrderBy(m => RectangleDistanceSquared(saved.Monitor.BoundsPixels, m.BoundsPixels))
										 .ThenBy(m => m.IsPrimary ? 0 : 1).First();
		var work = target.WorkAreaPixels;
		if (!work.IsValid)
		{
			throw new ArgumentException("Invalid current monitor working area.", nameof(available));
		}

		// Step 2: DIPs -> physical pixels using the target monitor's DPI (per axis). The saved
		// offset is relative to the work-area origin, so adding the target work-area origin keeps
		// the logical position even when the monitor has changed.
		if (target.DpiX == 0 || target.DpiY == 0)
		{
			throw new ArgumentException("A monitor reports an invalid (zero) DPI.", nameof(available));
		}

		var factorX = target.DpiX / ReferenceDpi;
		var factorY = target.DpiY / ReferenceDpi;
		var x = work.X + saved.BoundsDip.X * factorX;
		var y = work.Y + saved.BoundsDip.Y * factorY;
		var w = saved.BoundsDip.Width * factorX;
		var h = saved.BoundsDip.Height * factorY;
		if (isResizable)
		{
			// Step 3: resize to fit. The minimum is at least one pixel so that the size stays valid.
			var minWidth = Math.Max(1, (constraints?.MinWidthDip ?? 0) * factorX);
			var minHeight = Math.Max(1, (constraints?.MinHeightDip ?? 0) * factorY);
			// An unavoidable oversize minimum remains oversize; keep a usable caption.
			w = Math.Max(Math.Min(work.Width, w), minWidth);
			h = Math.Max(Math.Min(work.Height, h), minHeight);
		}

		// Step 4: a window that is still larger than the work area (fixed size, or minimum size
		// larger than the work area) is centered on the affected axis.
		if (w > work.Width || h > work.Height)
		{
			// Fixed/oversize windows: center; don't silently distort content.
			if (w > work.Width)
			{
				x = work.X + (work.Width - w) / 2;
			}

			if (h > work.Height)
			{
				y = work.Y + (work.Height - h) / 2;
			}
		}

		// Step 5: a window that fits must be completely visible. For an oversized fixed window,
		// preserve its centered position rather than moving its title bar away.
		if (w <= work.Width)
		{
			x = Clamp(x, work.X, work.X + work.Width - w);
		}

		if (h <= work.Height)
		{
			y = Clamp(y, work.Y, work.Y + work.Height - h);
		}

		return new ResolvedWindowPlacement
		{
			Monitor = target,
			BoundsPixels = new PlacementRect(x, y, w, h),
			IsMaximized = saved.IsMaximized,
			UsedFallbackMonitor = matched is null
		};
	}

	/// <summary>
	///     Matches the original monitor without mistaking a reassigned GDI name for the
	///     same physical device. A unique monitor interface path takes precedence. If both
	///     snapshots provide different paths, the display name must not override that conflict.
	///     Older saved placements containing only a device name retain their previous behavior.
	///     Duplicate interface paths are considered ambiguous and lead to geometric fallback.
	/// </summary>
	/// <param name="saved">Previously persisted monitor description.</param>
	/// <param name="available">Current monitor enumeration.</param>
	/// <returns>A uniquely matched current monitor or null if no safe identity match exists.</returns>
	/// <remarks>
	///     Matching order: (1) interface path, if the saved monitor has one: exactly one match is
	///     returned, more than one yields null, and no match continues with the name. (2) GDI name,
	///     accepted only if the interface paths are not both present and different. In both cases
	///     comparisons ignore case, and an ambiguous result (two candidates) yields null.
	/// </remarks>
	private static MonitorSnapshot? MatchMonitor(MonitorSnapshot saved, IReadOnlyList<MonitorSnapshot> available)
	{
		if (!string.IsNullOrWhiteSpace(saved.MonitorDevicePath))
		{
			// Take(2) is enough to tell "unique" from "ambiguous".
			var byPath = available.Where(m =>
									  string.Equals(saved.MonitorDevicePath, m.MonitorDevicePath, StringComparison.OrdinalIgnoreCase))
								  .Take(2).ToArray();
			if (byPath.Length == 1)
			{
				return byPath[0];
			}

			if (byPath.Length > 1)
			{
				return null;
			}
		}

		if (string.IsNullOrWhiteSpace(saved.DeviceName))
		{
			return null;
		}

		// Name match. If both sides know an interface path, the paths must also agree; this rejects
		// a name that Windows has reassigned to a different physical monitor. A missing path on either
		// side (legacy data, VM/RDP) leaves the name as the only available identity.
		var byName = available.Where(m =>
								  string.Equals(saved.DeviceName, m.DeviceName, StringComparison.OrdinalIgnoreCase) &&
								  (string.IsNullOrWhiteSpace(saved.MonitorDevicePath) ||
								   string.IsNullOrWhiteSpace(m.MonitorDevicePath) ||
								   string.Equals(saved.MonitorDevicePath, m.MonitorDevicePath, StringComparison.OrdinalIgnoreCase)))
							  .Take(2).ToArray();
		return byName.Length == 1 ? byName[0] : null;
	}

	/// <summary>
	///     Calculates squared edge-to-edge distance between two rectangles, returning zero for
	///     overlapping or touching rectangles. This selects an adjacent monitor over a distant monitor.
	/// </summary>
	/// <param name="a">First rectangle in virtual-desktop pixels.</param>
	/// <param name="b">Second rectangle in the same coordinate system.</param>
	/// <returns>The squared gap in pixels; the square root is not needed for comparisons.</returns>
	private static double RectangleDistanceSquared(PlacementRect a, PlacementRect b)
	{
		// Per axis, the gap is positive only if one rectangle lies completely before the other.
		var dx = Math.Max(0, Math.Max(a.X - (b.X + b.Width), b.X - (a.X + a.Width)));
		var dy = Math.Max(0, Math.Max(a.Y - (b.Y + b.Height), b.Y - (a.Y + a.Height)));
		return dx * dx + dy * dy;
	}

	/// <summary>
	///     Clamps a value inclusively between two endpoints. The endpoints may be passed in either
	///     order, so a degenerate range (for example a window just larger than the area) does not throw.
	/// </summary>
	/// <param name="value">The value to restrict.</param>
	/// <param name="minimum">One endpoint of the allowed range.</param>
	/// <param name="maximum">The other endpoint of the allowed range.</param>
	/// <returns>The value restricted to the range.</returns>
	private static double Clamp(double value, double minimum, double maximum)
	{
		return Math.Clamp(value, Math.Min(minimum, maximum), Math.Max(minimum, maximum));
	}
}
