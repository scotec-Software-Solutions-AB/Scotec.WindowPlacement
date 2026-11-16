---
name: win32-dpi
description: Analyze Win32 HWND geometry, DPI awareness, monitor enumeration and native placement APIs.
---
# Win32 DPI skill

Read `NativeMethods.cs`, `DpiAwarenessScope.cs`, `MonitorProvider.cs` and `Win32WindowPlacementService.cs` first.

- Verify Win32 declarations, pointer sizes, `SetLastError`, Unicode and monitor structures.
- Distinguish DPI-virtualized `GetWindowRect`, DWM visible frame bounds and `GetWindowPlacement` workspace coordinates.
- Maintain a consistent physical-coordinate view for native operations; do not confuse window DPI with monitor DPI.
- Scope thread DPI-awareness changes synchronously, check return values and always restore the previous awareness context.
- Do not change process awareness, create HWNDs, call WPF/Avalonia APIs or `await` while the scope is active.
- Recognize that `SetWindowPos` can trigger synchronous messages, while changing thread awareness alone should not repaint.
- Capture monitor interface paths best effort; do not claim they are stable across VMs, docks and RDP.
- Separate native Windows tests from deterministic geometry tests; cite remaining environment-specific limitations.
