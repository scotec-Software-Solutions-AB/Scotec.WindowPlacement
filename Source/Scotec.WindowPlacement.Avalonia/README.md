# Scotec.WindowPlacement.Avalonia

Avalonia adapter for `Scotec.WindowPlacement`. It adds one extension method, `ResolvedWindowPlacement.ApplyTo(Window)`, which applies a resolved placement to an Avalonia `Window` on Windows.

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
| `BoundsPixels.Width`, `Height` (physical pixels) | `Window.Width`, `Height` (logical units) | `pixels * 96 / monitor DPI`, per axis, using `Monitor.DpiX` and `DpiY`. |
| `IsMaximized` | `Window.WindowState` | Normal bounds are applied first, then `Maximized`. |

It also sets `WindowStartupLocation.Manual` and `SizeToContent.Manual`.

## Requirements and exceptions

- Call on the Avalonia UI thread after the window has a platform handle, preferably before it is shown.
- `PlatformNotSupportedException` outside Windows.
- `ArgumentNullException` for null arguments.
- `ArgumentException` for invalid bounds or DPI, or a position outside the `Int32` range.

## Limitations

- Avalonia width and height may describe content size rather than the native outer rectangle, depending on decorations and platform. Use `IWindowPlacementService.ApplyPlacement` with the HWND for exact outer bounds.
- A DPI change when the window moves to another monitor can trigger further layout. Mixed-DPI and multi-monitor behavior needs manual verification on Windows; it is not covered by the unit tests.
