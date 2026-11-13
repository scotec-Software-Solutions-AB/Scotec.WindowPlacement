using System;
using System.Collections.Generic;
using System.Linq;

namespace Scotec.WindowPlacement;

/// <summary>
///     Pure-geometry implementation of display fallback, monitor-relative DIP conversion,
///     and accessibility rules. This class does not access HWNDs and can be unit-tested using synthetic
///     monitor topologies.
/// </summary>
public static class WindowPlacementResolver
{
    /// <summary>
    ///     Resolves a persisted placement to physical screen coordinates on a current monitor.
    ///     Prefers a unique device-interface path match, then a non-conflicting GDI display-name
    ///     match, then the closest display rectangle with the primary display breaking ties.
    ///     Does not modify the persisted input.
    /// </summary>
    /// <param name="saved">Original saved placement.</param>
    /// <param name="available">Current monitors; must contain at least one entry.</param>
    /// <param name="isResizable">Whether the window can be resized.</param>
    /// <param name="constraints">Optional minimum logical dimensions when resizing.</param>
    /// <returns>A current physical screen rectangle and monitor.</returns>
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

        var matched = MatchMonitor(saved.Monitor, available);
        var target = matched ?? available.OrderBy(m => RectangleDistanceSquared(saved.Monitor.BoundsPixels, m.BoundsPixels))
                                         .ThenBy(m => m.IsPrimary ? 0 : 1).First();
        var work = target.WorkAreaPixels;
        if (!work.IsValid)
        {
            throw new ArgumentException("Invalid current monitor working area.", nameof(available));
        }

        var factorX = target.DpiX / 96d;
        var factorY = target.DpiY / 96d;
        var x = work.X + saved.BoundsDip.X * factorX;
        var y = work.Y + saved.BoundsDip.Y * factorY;
        var w = saved.BoundsDip.Width * factorX;
        var h = saved.BoundsDip.Height * factorY;
        if (isResizable)
        {
            var minWidth = Math.Max(1, (constraints?.MinWidthDip ?? 0) * factorX);
            var minHeight = Math.Max(1, (constraints?.MinHeightDip ?? 0) * factorY);
            // An unavoidable oversize minimum remains oversize; keep a usable caption.
            w = Math.Max(Math.Min(work.Width, w), minWidth);
            h = Math.Max(Math.Min(work.Height, h), minHeight);
        }

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

        // A window that fits must be completely visible. For an oversized fixed window,
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
    private static MonitorSnapshot? MatchMonitor(MonitorSnapshot saved, IReadOnlyList<MonitorSnapshot> available)
    {
        if (!string.IsNullOrWhiteSpace(saved.MonitorDevicePath))
        {
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
    private static double RectangleDistanceSquared(PlacementRect a, PlacementRect b)
    {
        var dx = Math.Max(0, Math.Max(a.X - (b.X + b.Width), b.X - (a.X + a.Width)));
        var dy = Math.Max(0, Math.Max(a.Y - (b.Y + b.Height), b.Y - (a.Y + a.Height)));
        return dx * dx + dy * dy;
    }

    /// <summary>Clamps a value inclusively between ordered endpoints.</summary>
    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Clamp(value, Math.Min(minimum, maximum), Math.Max(minimum, maximum));
    }
}
