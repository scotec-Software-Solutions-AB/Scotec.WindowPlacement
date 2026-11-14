# Scotec.WindowPlacement — Windows window placement library, WPF sample and Avalonia adapter

This repository contains a reusable Windows library (net8.0-windows and net10.0-windows) to capture a native window's current normal placement, resolve that placement against a changed monitor configuration, and optionally apply it to the window. There is no WPF/Avalonia dependency in the core. The WPF and Avalonia adapters and the dialog examples are separate projects.

## Projects

- `Source/Scotec.WindowPlacement`: Win32 HWND capture and apply, display enumeration, serializable placement models and pure placement resolver.
- `Source/Scotec.WindowPlacement.Wpf`: `WindowPlacementExtensions.ApplyTo` extension that applies the exact native physical outer bounds through the window handle (no DIP conversion). A hidden window stays hidden; a maximized placement is set through `Window.WindowState`.
- `Source/Scotec.WindowPlacement.Avalonia`: `WindowPlacementExtensions.ApplyTo` extension that sets an initial physical `PixelPoint` position and a logical size, applies the exact outer bounds natively after the window has opened and then pins `Width`/`Height` to the resulting client size.
- `Source/Samples/WindowPlacement.Sample.Wpf`: opens a non-modal resizable dialog, captures its HWND placement in `Closing` (and when its owner closes), writes JSON to `%LOCALAPPDATA%/Scotec/WindowPlacementSample/dialog.json`, and restores through the WPF extension on `SourceInitialized`.
- `Source/Samples/WindowPlacement.Sample.Avalonia`: opens a non-modal resizable Avalonia dialog, restores through the Avalonia extension and captures its placement in `Closing`, writing JSON to `%LOCALAPPDATA%/Scotec/WindowPlacementSampleAvalonia/dialog.json`.
- `Source/Tests/WindowPlacement.Tests`: standalone dependency-free geometry smoke tests; no third-party test NuGet packages.

## Usage

```csharp
IWindowPlacementService service = new Win32WindowPlacementService();
SavedWindowPlacement saved = service.GetCurrentPlacement(hwnd);
// Serialize `saved` to your preferred persistence provider.
ResolvedWindowPlacement resolved = service.GetPlacement(saved, hwnd);
resolved.ApplyTo(wpfWindow); // WPF adapter after SourceInitialized
// Or: service.ApplyPlacement(hwnd, resolved); // native HWND outer bounds
```

The core uses monitor-work-area-relative **DIPs** (96 units per inch) for saved coordinates and **physical virtual-screen pixels** for resolved coordinates. The normal/restored window rectangle is saved even if currently maximized; a window that was maximized and then minimized is saved as maximized. A monitor that reports a zero DPI (possible with a custom monitor provider) is rejected instead of causing a division by zero. A missing monitor falls back to the geometrically closest available monitor, preserving the same logical offset from that monitor's work area where possible. Windows display names such as `\\.\DISPLAY2` are matched best-effort but are not guaranteed physical hardware identifiers.

Resizable windows are reduced to fit the target working area, respecting provided minimum dimensions where feasible. Oversized fixed windows retain their size and are centered on the working area; in this impossible-to-fit situation some window content will remain off-screen. Windows that fit are fully clamped into the usable working area. No monitor topology is changed, and `GetPlacement` never overwrites its saved input.

## Important Windows / framework limitations

1. **DPI contexts (updated)**: `Win32MonitorProvider.GetMonitors()`, `Win32WindowPlacementService.GetCurrentPlacement()` and `ApplyPlacement()` each enter a **temporary per-monitor-V2 DPI-aware thread context** (falling back to per-monitor V1 where required), and restore the exact previous thread context in `Dispose()`/`finally` semantics. A failed switch throws rather than using inaccurate virtualized coordinates. The code does **not** modify the host's process DPI awareness, nor the awareness of existing HWNDs. No asynchronous operations occur inside the scope. `GetDpiForWindow` remains informational: a system-aware HWND can report system DPI even on another monitor. **Important:** `ApplyPlacement` uses `SetWindowPos`, and `ShowWindow` only for an already visible window that is maximized or minimized. Both can deliver native messages synchronously while the temporary context is active. The implementation does not call WPF/Avalonia APIs during the scope; test with your host's UI lifecycle and prefer framework adapters if the native message behavior is problematic. Requires Windows 10+. A window that is not yet visible stays hidden and is not maximized natively; the caller must maximize it and should apply the placement again once it is visible, because a hidden window receives no DPI change for the target monitor.
2. **WPF adapter**: the window's current `HwndSource` DPI can differ from the DPI of the monitor the placement resolves to (for example a dialog created on a 150% monitor and restored to a 100% monitor), so converting physical pixels to DIPs with the current transform gives a wrong size. The WPF adapter therefore does not convert; it applies the exact physical outer bounds through the core `ApplyPlacement(hwnd, ...)`, which re-applies them once if the window DPI changed during the move. When called from `SourceInitialized` the window is still hidden and stays hidden, which avoids black content until WPF has rendered. A hidden window receives no DPI change for the target monitor, so the adapter applies the bounds again (posted to the dispatcher) after `Loaded`, after each `DpiChanged` and after `ContentRendered`. A 100% main window with a dialog on a 150% display was verified manually; this is not covered by automated tests.
3. **Normal rectangle**: `GetWindowPlacement` stores `rcNormalPosition` in workspace coordinates for ordinary top-level windows. The native implementation maps that to screen coordinates using the monitor work-area offset. For a maximized or minimized window the owning monitor is queried with `MonitorFromWindow`, because a minimized window's rectangle is off-screen. Customized tool windows with `WS_EX_TOOLWINDOW`, or apps that control their own placement, may require a different handling strategy. Test maximized windows when deploying in a real host.
4. **Window size constraints**: A `WS_THICKFRAME` style is a heuristic for resizability. It does not report application-specific `WM_GETMINMAXINFO` constraints, so optionally pass `WindowSizeConstraints` (minimum width/height in DIPs). For nonstandard custom chrome, the caller may need to override style-based assumptions.
5. **Outer versus client dimensions**: Native placement is an outer-window rectangle (title bar and borders included). The WPF adapter applies it natively. Avalonia `Width`/`Height` are client sizes, so the Avalonia adapter applies the outer bounds natively after the window has opened and then pins `Width`/`Height` to the resulting client size; otherwise the window would grow by its frame size on every restore. Windows may still constrain the size.
6. **Display identity**: Monitor snapshots now optionally include the Windows monitor interface path obtained with `EnumDisplayDevicesW(..., EDD_GET_DEVICE_INTERFACE_NAME)`. The resolver prefers a unique matching interface path before GDI display names, rejecting name-only matches when both identities are present but conflict. Otherwise it uses geometric proximity, breaking ties in favor of the primary monitor. Interface paths are not guaranteed persistent for RDP, virtual monitors, driver changes or hardware replacement; geometry remains the fallback. Older serialized snapshots without the optional property remain readable.
7. **Persistence**: The samples save on `Closing` (the WPF sample also saves when its owner closes, because closing an owner destroys an owned, for example minimized, dialog without raising its `Closing` event), suppress persistence errors, and use a temp file plus an overwrite move on the same volume. It does not update placement continuously or react to live monitor disconnects while a dialog is open. Multi-instance concurrent writes to the same file are not coordinated.
8. **UI thread**: Native windows must be accessed/modified from the window's owning UI thread. Restore after HWND creation, ideally before the window becomes visible; do not block the UI thread waiting for a dialog.
9. **Windows only**: This is an HWND-based component, not a cross-platform display abstraction. The Win32 service and both framework adapters throw `PlatformNotSupportedException` when invoked outside Windows. The target frameworks are `net8.0-windows` and `net10.0-windows`. Avalonia on Windows can use its platform HWND handle with the core service and the Avalonia extension to apply the resulting placement.

## Run on Windows

Open `Source/Scotec.WindowPlacement.slnx` with Visual Studio (with .NET desktop development) or run:

```powershell
dotnet run --project Source/Samples/WindowPlacement.Sample.Wpf
dotnet run --project Source/Samples/WindowPlacement.Sample.Avalonia
dotnet run --project Source/Tests/WindowPlacement.Tests -f net8.0-windows
```

The WPF sample depends only on .NET Desktop; the Avalonia sample uses Avalonia 11.3.22. Both persist a dialog placement as JSON beneath `%LOCALAPPDATA%\Scotec`.


## Avalonia integration

The solution also includes `Scotec.WindowPlacement.Avalonia` (Avalonia 11.3.22), a
separate optional adapter project. It depends on Avalonia but **the core service does not**.

```csharp
using Scotec.WindowPlacement.Avalonia;

SavedWindowPlacement saved = /* deserialize saved placement */;
ResolvedWindowPlacement resolved = placementService.GetPlacement(saved);
resolved.ApplyTo(avaloniaWindow);
```

`ApplyTo` uses the public Avalonia `Window` API plus the core native `ApplyPlacement`.
`Window.Position` is set from the absolute physical-pixel coordinates, and `Width`
and `Height` are first set from the physical outer size converted with the
destination monitor's DPI (an estimate, because Avalonia sizes are client sizes).
The adapter also sets manual startup positioning and manual size-to-content.
Avalonia re-applies `Width`/`Height` while showing the window, so the exact outer
bounds are applied natively once the window has opened (posted behind the initial
layout pass); afterwards `Width`/`Height` are pinned to the resulting client size
so that the window does not grow on later restores. The maximized state is restored
by the native apply. Call on the Avalonia UI thread, preferably before showing the
window.

The result was confirmed manually on a 150% / 100% two-monitor setup; there are no
automated adapter tests. Verify behavior with other DPI combinations and when
Avalonia is hosted inside another application. A brief flash at the estimated size before the window
has opened is possible.

## Naming and supported platforms

The three reusable library projects are `Scotec.WindowPlacement`, `Scotec.WindowPlacement.Wpf`, and `Scotec.WindowPlacement.Avalonia`. Both adapter projects expose a `WindowPlacementExtensions` class within their own framework-specific namespaces. The solution deliberately does **not** include `.Win32` or `.Extensions` in assembly names. Windows is the only supported operating system; non-Windows calls to the native service and adapters throw `PlatformNotSupportedException`. No macOS/Linux support is planned or claimed.

## DPI scope verification

- A thread-local DPI change by itself does not repaint windows. Applying a new placement *does* move or size the HWND, which can invoke repaint and layout.
- The native query APIs run in a per-monitor context to obtain consistent physical desktop pixels even under a system-DPI-aware host process. Monitor snapshots have effective DPI from `GetDpiForMonitor` under that same context.
- The WPF/Avalonia adapters call ApplyPlacement (which enters and leaves its own short DPI scope) and framework APIs **outside** any native DPI scope. ApplyPlacement re-applies the physical bounds once if the window DPI changed during the move. Test in a real multi-monitor host application.
- Monitor identity remains best-effort. `GetWindowPlacement` workspace conversion and nonstandard window chrome should be validated with taskbars on secondary monitors.
- The tests include pure geometry smoke tests plus a Windows-only check that monitor enumeration restores the original thread DPI-awareness context. There are no automated WPF/Avalonia runtime integration tests; the adapter behavior was verified manually only.

## Display-name reassignment

`MonitorSnapshot.MonitorDevicePath` is optional to preserve compatibility with old saved JSON. On Windows, `Win32MonitorProvider` tries to obtain this monitor interface path for each active display. When the GDI name changes but the interface path remains stable, the resolver restores to the original physical monitor. When the old GDI name is reassigned to a different monitor, a conflicting nonempty interface path prevents a false match. If the monitor interface is unavailable, the existing display-name/geometry fallback remains. The service does not overwrite the original saved placement during fallback.

**Limitations:** The monitor path is not a guaranteed globally stable hardware serial identifier. Two identical, mirrored or virtual monitor instances can have indistinguishable or unstable identifiers. Persistent per-monitor identification across all docks and remote desktop sessions requires additional platform-specific policy and cannot be guaranteed.
