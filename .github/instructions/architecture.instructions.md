---
description: Architecture boundaries and public contracts for the window placement core and framework adapters.
applyTo: "**/*.cs"
---
# Architecture

- Core `Scotec.WindowPlacement` works on Windows HWNDs and contains Win32 interop, monitor enumeration, serialized models and placement resolution. It must not reference WPF or Avalonia.
- `Scotec.WindowPlacement.Wpf` and `.Avalonia` depend on the core, not on each other. Keep each adapter's `WindowPlacementExtensions` in its own namespace.
- Separate pure monitor/rectangle selection from native enumeration and window mutation. Test geometry without actual screens.
- Retain two coordinate contracts: saved bounds are **working-area-relative DIPs**; resolved bounds are **absolute physical-pixel screen coordinates**. Describe conversions explicitly.
- `GetPlacement` must not change an HWND, mutate input, persist settings or silently replace original monitor identity during fallback. `ApplyPlacement` is an explicit side-effecting operation.
- Monitor names and interface paths are best effort; reject known identity conflicts and use deterministic geometric fallback.
- No speculative portability abstraction: Windows is the only supported platform. Keep any platform checks explicit.
- Preserve serialized property names and optional additions to support older saved JSON. Document intentional public API changes.
