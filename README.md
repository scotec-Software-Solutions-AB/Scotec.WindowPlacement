# Scotec.WindowPlacement — Windows window placement library, WPF sample and Avalonia adapter

This repository contains a reusable .NET 8 Windows library to capture a native window's current normal placement, resolve that placement against a changed monitor configuration, and optionally apply it to the window. There is no WPF/Avalonia dependency in the core. The WPF and Avalonia adapters and the modal dialog example are separate projects.

## Projects

- `src/Scotec.WindowPlacement`: Win32 HWND capture and apply, display enumeration, serializable placement models and pure placement resolver.
- `src/Scotec.WindowPlacement.Wpf`: `WindowPlacementExtensions.ApplyTo` extension converting native physical-pixel geometry to WPF DIP window properties.
- `src/Scotec.WindowPlacement.Avalonia`: `WindowPlacementExtensions.ApplyTo` extension converting physical positions to Avalonia `PixelPoint` and sizes to logical DIPs.
- `samples/WindowPlacement.Sample.Wpf`: opens a modal resizable dialog, captures its HWND placement in `Closing`, writes JSON to `%LOCALAPPDATA%/Scotec/WindowPlacementSample/dialog.json`, and restores through the WPF extension on `SourceInitialized`.
- `tests/WindowPlacement.Tests`: standalone dependency-free geometry smoke tests; no third-party test NuGet packages.

## Usage

```csharp
IWindowPlacementService service = new Win32WindowPlacementService();
SavedWindowPlacement saved = service.GetCurrentPlacement(hwnd);
// Serialize `saved` to your preferred persistence provider.
ResolvedWindowPlacement resolved = service.GetPlacement(saved, hwnd);
resolved.ApplyTo(wpfWindow); // WPF adapter after SourceInitialized
// Or: service.ApplyPlacement(hwnd, resolved); // native HWND outer bounds
```

The core uses monitor-work-area-relative **DIPs** (96 units per inch) for saved coordinates and **physical virtual-screen pixels** for resolved coordinates. The normal/restored window rectangle is saved even if currently maximized. A missing monitor falls back to the geometrically closest available monitor, preserving the same logical offset from that monitor's work area where possible. Windows display names such as `\\.\DISPLAY2` are matched best-effort but are not guaranteed physical hardware identifiers.

Resizable windows are reduced to fit the target working area, respecting provided minimum dimensions where feasible. Oversized fixed windows retain their size and are centered on the working area; in this impossible-to-fit situation some window content will remain off-screen. Windows that fit are fully clamped into the usable working area. No monitor topology is changed, and `GetPlacement` never overwrites its saved input.

## Important Windows / framework limitations

1. **DPI contexts (updated)**: `Win32MonitorProvider.GetMonitors()`, `Win32WindowPlacementService.GetCurrentPlacement()` and `ApplyPlacement()` each enter a **temporary per-monitor-V2 DPI-aware thread context** (falling back to per-monitor V1 where required), and restore the exact previous thread context in `Dispose()`/`finally` semantics. A failed switch throws rather than using inaccurate virtualized coordinates. The code does **not** modify Revit's or any other host's process DPI awareness, nor the awareness of existing HWNDs. No asynchronous operations occur inside the scope. `GetDpiForWindow` remains informational: a system-aware HWND can report system DPI even on another monitor. **Important:** `ApplyPlacement` uses `ShowWindow` and `SetWindowPos`, which can deliver native messages synchronously while the temporary context is active. The implementation does not call WPF/Avalonia APIs during the scope; test with your host's UI lifecycle and prefer framework adapters if the native message behavior is problematic. Requires Windows 10+.
2. **WPF adapter**: WPF `Left`, `Top`, `Width` and `Height` use DIPs, and `HwndSource.CompositionTarget.TransformFromDevice` reflects the *current* HwndSource DPI. If a window is created on one DPI and restored to a different-DPI monitor, WPF may recalculate its DPI/layout after it moves. This adapter is best effort and may require post-move corrections for precise physical geometry. The core `ApplyPlacement(hwnd, ...)` avoids framework property conversions for precise outer-frame placement, subject to DPI awareness.
3. **Normal rectangle**: `GetWindowPlacement` stores `rcNormalPosition` in workspace coordinates for ordinary top-level windows. The native implementation maps that to screen coordinates using the monitor work-area offset. Customized tool windows with `WS_EX_TOOLWINDOW`, or apps that control their own placement, may require a different handling strategy. Test maximized windows when deploying in a real host.
4. **Window size constraints**: A `WS_THICKFRAME` style is a heuristic for resizability. It does not report application-specific `WM_GETMINMAXINFO` constraints, so optionally pass `WindowSizeConstraints` (minimum width/height in DIPs). For nonstandard custom chrome, the caller may need to override style-based assumptions.
5. **Outer versus WPF dimensions**: Native placement is an outer-window rectangle. WPF framework sizing or custom chrome can produce a slightly different outer rectangle, and Windows may constrain size; the WPF sample is a demonstration, not a guarantee of pixel-exact restoration.
6. **Display identity**: Monitor snapshots now optionally include the Windows monitor interface path obtained with `EnumDisplayDevicesW(..., EDD_GET_DEVICE_INTERFACE_NAME)`. The resolver prefers a unique matching interface path before GDI display names, rejecting name-only matches when both identities are present but conflict. Otherwise it uses geometric proximity, breaking ties in favor of the primary monitor. Interface paths are not guaranteed persistent for RDP, virtual monitors, driver changes or hardware replacement; geometry remains the fallback. Older serialized snapshots without the optional property remain readable.
7. **Persistence**: The sample saves on `Closing`, suppresses persistence errors, and uses a temp file plus an overwrite move on the same volume. It does not update placement continuously or react to live monitor disconnects while a modal dialog is open. Multi-instance concurrent writes to the same file are not coordinated.
8. **UI thread**: Native windows must be accessed/modified from the window's owning UI thread. Restore after HWND creation, ideally before the window becomes visible; do not block the UI thread waiting for a modal dialog.
9. **Windows only**: This is an HWND-based component, not a cross-platform display abstraction. The Win32 service and both framework adapters throw `PlatformNotSupportedException` when invoked outside Windows. The target framework is `net8.0-windows`. Avalonia on Windows can use its platform HWND handle with the core service and the Avalonia extension to apply the resulting placement.

## Run on Windows

Open `WindowPlacementSolution.sln` with Visual Studio 2022 (with .NET desktop development) or run:

```powershell
dotnet run --project samples/WindowPlacement.Sample.Wpf
dotnet run --project tests/WindowPlacement.Tests
```

The sample is intentionally dependency-free apart from .NET Desktop. No build was performed in the artifact creation environment.


## Avalonia integration

The solution also includes `Scotec.WindowPlacement.Avalonia` (Avalonia 11.3.22), a
separate optional adapter project. It depends on Avalonia but **the core service does not**.

```csharp
using Scotec.WindowPlacement.Avalonia;

SavedWindowPlacement saved = /* deserialize saved placement */;
ResolvedWindowPlacement resolved = placementService.GetPlacement(saved);
resolved.ApplyTo(avaloniaWindow);
```

`ApplyTo` uses the public Avalonia `Window` API. `Window.Position` is set directly
from the absolute physical-pixel coordinates; `Width` and `Height` are converted
from physical pixels to DIPs using the selected monitor's DPI. The adapter also
sets manual startup positioning and manual size-to-content, and restores maximized
state as needed. Call on the Avalonia UI thread after the platform handle exists,
ideally before showing the window.

As with WPF, Avalonia's logical Width and Height are not guaranteed to exactly
match the native outer window bounds when non-client decorations are involved.
For pixel-exact native outer bounds, use the core HWND `ApplyPlacement` instead.
The adapter has not been build- or runtime-tested here. Verify behavior with
per-monitor DPI changes and when Avalonia is hosted by Revit.

## Naming and supported platforms

The three reusable library projects are `Scotec.WindowPlacement`, `Scotec.WindowPlacement.Wpf`, and `Scotec.WindowPlacement.Avalonia`. Both adapter projects expose a `WindowPlacementExtensions` class within their own framework-specific namespaces. The solution deliberately does **not** include `.Win32` or `.Extensions` in assembly names. Windows is the only supported operating system; non-Windows calls to the native service and adapters throw `PlatformNotSupportedException`. No macOS/Linux support is planned or claimed.

## DPI scope verification / Revit

- A thread-local DPI change by itself does not repaint windows. Applying a new placement *does* move or size the HWND, which can invoke repaint and layout.
- The native query APIs run in a per-monitor context to obtain consistent physical desktop pixels even under a system-DPI-aware Revit process. Monitor snapshots have effective DPI from `GetDpiForMonitor` under that same context.
- The WPF/Avalonia adapters still use their framework APIs **after** native DPI scopes have been restored. Mixed DPI movement remains framework-dependent; a WPF source's current DPI can differ from the destination DPI until its window moves. Test on a real multi-monitor Revit installation.
- Monitor identity remains best-effort. `GetWindowPlacement` workspace conversion and nonstandard window chrome should be validated with taskbars on secondary monitors.
- The tests include pure geometry smoke tests plus a Windows-only check that monitor enumeration restores the original thread DPI-awareness context. No WPF/Avalonia/Revit runtime integration tests or compilation were performed.

## Display-name reassignment

`MonitorSnapshot.MonitorDevicePath` is optional to preserve compatibility with old saved JSON. On Windows, `Win32MonitorProvider` tries to obtain this monitor interface path for each active display. When the GDI name changes but the interface path remains stable, the resolver restores to the original physical monitor. When the old GDI name is reassigned to a different monitor, a conflicting nonempty interface path prevents a false match. If the monitor interface is unavailable, the existing display-name/geometry fallback remains. The service does not overwrite the original saved placement during fallback.

**Limitations:** The monitor path is not a guaranteed globally stable hardware serial identifier. Two identical, mirrored or virtual monitor instances can have indistinguishable or unstable identifiers. Persistent per-monitor identification across all docks and remote desktop sessions requires additional platform-specific policy and cannot be guaranteed.
