# Scotec.WindowPlacement.Avalonia

which applies a resolved placement to an Avalonia `Window` on Windows, using Avalonia properties and the native outer bounds together.

## Usage

```csharp
var service = new Win32WindowPlacementService();

// Capture: use the window's platform handle.
nint hwnd = window.TryGetPlatformHandle()?.Handle ?? 0;
SavedWindowPlacement saved = service.GetCurrentPlacement(hwnd);

// Restore: resolve, then apply on the UI thread once the window has a platform handle.
ResolvedWindowPlacement resolved = service.GetPlacement(saved, hwnd);
resolved.ApplyTo(window);
```

## Conversion rules

| Input | Avalonia property | Conversion |
| --- | --- | --- |
| `BoundsPixels.X`, `Y` (physical screen pixels, may be negative) | `Window.Position` (`PixelPoint`) | Rounded to the nearest integer (away from zero); no DPI scaling. |
| `BoundsPixels.Width`, `Height` (physical **outer** pixels) | `Window.Width`, `Height` (logical **client** units) | Initial estimate `pixels * 96 / monitor DPI`, per axis, using `Monitor.DpiX` and `DpiY`. Replaced by the pinned client size described below. |
| `IsMaximized` | `Window.WindowState` | Normal bounds are applied first, then `Maximized`. |

It also sets `WindowStartupLocation.Manual` and `SizeToContent.Manual`.

## Why the native apply

Resolved bounds are the native **outer** rectangle (title bar and borders included), but Avalonia `Width`/`Height` are **client** sizes. Avalonia also re-applies `Width`/`Height` while showing the window. Therefore:

1. `Position`, `Width` and `Height` are set as an initial estimate.
2. If the window is already visible, the exact outer bounds are applied immediately with `IWindowPlacementService.ApplyPlacement`. Otherwise this happens once after the `Opened` event, posted at `DispatcherPriority.Loaded` so that it runs after the initial layout pass.
3. After the native apply, `Width` and `Height` are set to the resulting `ClientSize`. Without this, a later layout pass would use the outer size as the client size and the window would grow by its frame size on every restore.

## Requirements and exceptions

- Call on the Avalonia UI thread, preferably before the window is shown. If the window is not visible yet, the native apply is deferred until it has opened; keep the window instance alive until then.
- `PlatformNotSupportedException` outside Windows.
- `ArgumentNullException` for null arguments.
- `ArgumentException` for invalid bounds or DPI, or a position outside the `Int32` range.

## Limitations

- A brief flash at the estimated size before the window has opened is possible.
- The pinned `Width`/`Height` are the client size at the DPI after the native move; a later DPI change is handled by Avalonia.
- Confirmed manually on a 150% / 100% two-monitor setup. Other mixed-DPI and multi-monitor combinations and Revit hosting are not covered by automated tests and need manual verification on Windows.
