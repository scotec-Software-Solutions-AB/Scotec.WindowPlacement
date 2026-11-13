# GitHub Copilot instructions — Scotec.WindowPlacement

This repository is a **Windows-only .NET 8 library** for capturing, resolving and restoring native window placement across multi-monitor configurations and DPI settings. It is **not** a general-purpose window manager, nor a cross-platform implementation.

## Inspect the repository first

- Read `README.md`, `WindowPlacementSolution.sln`, `Directory.Build.props`, and the relevant files under `src/`, `samples/`, and `tests/` before changes.
- Libraries: `src/Scotec.WindowPlacement` (Win32 HWND provider, DPI scope, monitor identity, resolver and models), `src/Scotec.WindowPlacement.Wpf` and `src/Scotec.WindowPlacement.Avalonia` (optional framework adapters).
- Sample: `samples/WindowPlacement.Sample.Wpf`. Tests: `tests/WindowPlacement.Tests` (inspect actual runner; do not assume xUnit).
- Target is `net8.0-windows`; check actual project files before changing target frameworks or NuGet dependencies.

## Required behavior and invariants

- `GetCurrentPlacement(nint hwnd)` captures the normal/restored window rectangle even if maximized, the original monitor, DPI and resizability; persist monitor-work-area-relative **DIPs**.
- `GetPlacement(savedPlacement, hwnd)` is resolution only: do **not** mutate the saved record or move a native window. It returns a target monitor and **absolute physical desktop-pixel** bounds.
- Prefer a uniquely matching monitor interface path, then a non-conflicting display device name, then geometrically nearest available monitor, with deterministic primary-monitor tie-breaking. Names and monitor paths may change (RDP/VM/docking); never treat them as guaranteed stable.
- When the original monitor disappears, preserve the saved relative logical offset on the chosen monitor when possible; clamp to its working area, including taskbar offsets.
- Resize resizable windows to fit, subject to known constraints. Preserve fixed-size window dimensions, center when necessary, and acknowledge that an oversized fixed dialog may remain partly off-screen.
- Preserve normal bounds separately from maximized state. Do not restore a dialog minimized by default.
- Use physical screen coordinates consistently inside native interop; distinguish `GetWindowRect` virtualization, DWM outer frame bounds, `WINDOWPLACEMENT` workspace coordinates and monitor work areas.
- Native Win32 DPI-awareness scopes must be short, synchronous and balanced with `try/finally`/`Dispose`; check failure, do not modify process DPI awareness, create windows, call framework APIs or `await` inside those scopes. Beware synchronous messages triggered by `SetWindowPos`.
- WPF window geometry uses DIPs; Avalonia `Window.Position` uses `PixelPoint` and sizes use logical units. Keep adapters separate and conversion-aware. Native outer bounds and framework window dimensions may differ.
- Public Windows-specific entry points should throw `PlatformNotSupportedException` outside Windows. Call HWND operations on the window-owning UI thread.

## Implementation and documentation

- Preserve public APIs, serialized placement compatibility (including optional `MonitorDevicePath`), and existing behavior unless the task explicitly requests a change.
- Follow existing C# naming, nullability and XML documentation conventions. Document **interfaces, classes, public and private methods**, including input/output coordinate systems, DPI, fallback policies, exceptions and threading assumptions.
- Keep the resolver deterministic/testable and native Win32 calls isolated. Prefer small changes over additional framework dependencies or unnecessary architecture.
- Do not silently swallow failures in the library. Sample-level best-effort persistence should be clearly distinguished from library behavior.

## Verification

- Run targeted tests/builds if available, and report what really ran. Geometry tests should cover monitor rename/reassignment/disconnect, DPI changes, negative monitor origins, work-area changes, fixed/resizable windows, maximize and old saved data.
- Real HWND and WPF/Avalonia behavior require Windows integration tests; multi-monitor/mixed-DPI VM/RDP scenarios cannot be established by pure unit tests.
- Do not claim a compile, test pass, platform behavior or benchmark without evidence.
