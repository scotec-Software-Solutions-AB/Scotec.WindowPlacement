---
name: ui-adapters
description: Implement WPF/Avalonia window-placement extensions with correct conversion and window lifecycle.
---
# WPF and Avalonia placement adapters

Inspect both `WindowPlacementExtensions.cs` files and WPF sample dialog before editing.

- Accept the core `ResolvedWindowPlacement` physical rectangle; convert to the target framework's coordinates.
- WPF `Left/Top/Width/Height` are DIPs; window source DPI can change when moved to another monitor, so cross-DPI placement must be integration-tested.
- Avalonia `Position` uses `PixelPoint` while `Width/Height` are logical values; respect destination scaling.
- Framework bounds can differ from Win32 outer-window rectangles due to chrome/size-to-content. Do not claim exact identity.
- Apply after a handle exists but ideally before showing, so the window is not visually repositioned after opening; run on the framework UI thread.
- Preserve maximized behavior and window sizing constraints; do not modify process DPI awareness.
- Keep WPF/Avalonia references outside the core; throw `PlatformNotSupportedException` on unsupported operating systems.
