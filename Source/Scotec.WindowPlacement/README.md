# Scotec.WindowPlacement

Framework-independent, Windows-only core library. It captures the normal placement of a native top-level window (HWND), resolves a saved placement against the currently attached monitors, and can apply the result to an HWND. It has no WPF or Avalonia dependency.

## Main types

| Type | Purpose |
| --- | --- |
| `IWindowPlacementService` | Public contract: `GetCurrentPlacement`, `GetPlacement`, `ApplyPlacement`. |
| `Win32WindowPlacementService` | Win32 implementation. Takes an optional `IMonitorProvider`. |
| `IMonitorProvider` / `Win32MonitorProvider` | Enumerates monitors as `MonitorSnapshot` values. |
| `WindowPlacementResolver` | Pure geometry resolver. It uses no HWNDs and can be tested with synthetic monitor layouts. |
| `SavedWindowPlacement` | Serializable record to persist. |
| `ResolvedWindowPlacement` | Result of resolution, in absolute physical pixels. |
| `MonitorSnapshot`, `PlacementRect`, `WindowSizeConstraints` | Supporting models. |

## Usage

```csharp
IWindowPlacementService service = new Win32WindowPlacementService();

// Capture on the window's UI thread, for example while closing.
SavedWindowPlacement saved = service.GetCurrentPlacement(hwnd);
// Serialize `saved` yourself (for example with System.Text.Json).

// Later: resolve against the current monitors. This neither changes `saved` nor moves the window.
ResolvedWindowPlacement resolved = service.GetPlacement(
	saved, hwnd, new WindowSizeConstraints { MinWidthDip = 400, MinHeightDip = 300 });

// Optional: apply to the native outer bounds (UI thread).
service.ApplyPlacement(hwnd, resolved);
```

For WPF or Avalonia windows, use the adapter packages `Scotec.WindowPlacement.Wpf` or `Scotec.WindowPlacement.Avalonia` instead of `ApplyPlacement`.

## Coordinate systems

- **Saved** (`SavedWindowPlacement.BoundsDip`): DIPs (96 per inch), relative to the saved monitor's work-area origin. The monitor snapshot itself (`BoundsPixels`, `WorkAreaPixels`) is in physical virtual-screen pixels.
- **Resolved** (`ResolvedWindowPlacement.BoundsPixels`): absolute physical virtual-desktop pixels. Origins can be negative for monitors left of or above the primary monitor.
- The normal (restored) rectangle is saved even if the window is maximized. `IsMaximized` is stored separately. Minimized windows are saved as normal. For a maximized or minimized window the owning monitor is queried with `MonitorFromWindow`, because a minimized window's rectangle is off-screen.

## Monitor matching

1. A uniquely matching monitor interface path (`MonitorDevicePath`).
2. A GDI display name (such as `\\.\DISPLAY2`) that does not conflict with a different known interface path.
3. The geometrically nearest available monitor; the primary monitor wins ties.

Names and interface paths can change with RDP, virtual machines, docking, driver changes or hardware replacement, so geometry is always the fallback. When the original monitor is missing, the relative logical offset is kept on the chosen monitor where possible and clamped to its work area (taskbar included).

## Sizing rules

- Resizable windows are reduced to fit the work area, respecting `WindowSizeConstraints` where feasible.
- Fixed-size windows keep their size and are centered when they do not fit. An oversized fixed window will remain partly off-screen.

## Serialization compatibility

`MonitorSnapshot.MonitorDevicePath` is optional. Placements saved without it still deserialize and use name and geometry matching.

## DPI awareness and threading

- `GetMonitors`, `GetCurrentPlacement` and `ApplyPlacement` temporarily switch the calling thread to a per-monitor-aware DPI context and restore the previous context in `finally`. A failed switch throws. Process DPI awareness is never changed, and no framework UI code is called inside the scope.
- `ApplyPlacement` uses `SetWindowPos`, and `ShowWindow` only for a window that is already visible; both may dispatch window messages synchronously while the temporary context is active. A hidden window stays hidden and is not maximized natively, so the caller must maximize it and should apply the placement again once the window is visible (a hidden window gets no DPI change for the target monitor). Verify this in your host application.
- Call HWND operations on the window-owning UI thread.
- Windows-specific entry points throw `PlatformNotSupportedException` on other operating systems.

## Limitations

- `WS_THICKFRAME` is used as the resizability heuristic. Application-specific `WM_GETMINMAXINFO` limits are not detected, so pass `WindowSizeConstraints`.
- Native placement describes the outer window rectangle. Custom chrome and framework sizing can differ slightly.
- Real HWND, multi-monitor and mixed-DPI behavior needs verification on Windows hardware or VMs; unit tests cannot establish it.
