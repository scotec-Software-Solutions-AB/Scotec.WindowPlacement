# WindowPlacement.Tests

Dependency-free console test runner for `Scotec.WindowPlacement`. It uses no test framework or third-party NuGet packages: `Program.cs` runs assertions against `WindowPlacementResolver` with synthetic monitor layouts and exits with a nonzero code on failure.

## Run

From the repository root on Windows:

```
dotnet run --project Source/Tests/WindowPlacement.Tests
```

On success it prints `All placement resolver and DPI context checks passed.`

## Covered scenarios

- Restore to the same monitor, and fall back to the nearest monitor when one is missing, keeping the monitor-relative offset.
- DPI change on the target monitor (relative position and size scaling).
- Resizable window reduced to fit a smaller monitor, and kept fully visible.
- Oversized fixed-size window keeps its size and is centered.
- Work-area offset such as a taskbar.
- Interface-path identity beating a reassigned GDI name, and a conflicting name rejected in favor of geometry fallback.
- Legacy placements without `MonitorDevicePath`, and an unavailable interface path falling back to geometry.
- The saved placement is never mutated by resolution.
- `Win32MonitorProvider.GetMonitors()` leaves the thread's DPI-awareness context unchanged (Windows only; skipped elsewhere).

## Not covered

These need Windows integration or manual verification:

- Real HWND capture and `ApplyPlacement`.
- WPF and Avalonia adapter behavior (native apply, Opened/Loaded deferral, client-size pinning); only checked manually with the samples.
- Real multi-monitor, mixed-DPI, RDP or VM topologies.
- Maximized-state restore on real windows.
- Negative virtual-screen origins and maximized saved data have no dedicated resolver cases yet; consider adding them.
