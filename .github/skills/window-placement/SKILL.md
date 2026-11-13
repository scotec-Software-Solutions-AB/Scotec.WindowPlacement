---
name: window-placement
description: Implement or review monitor-aware placement capture, resolution, saving and restoration in Scotec.WindowPlacement.
---
# Window placement skill

Read `README.md`, core `Models.cs`, `IWindowPlacementService.cs`, `WindowPlacementResolver.cs`, `Win32WindowPlacementService.cs` and relevant tests.

- Captured normal window rectangle must preserve monitor identity, DPI and monitor-work-area-relative DIP geometry; keep maximize state separate.
- Resolved placement must use absolute physical desktop pixels and selected target monitor. The resolver must not move a window or rewrite saved placement.
- Prefer unique monitor interface path, then safe device-name match, otherwise nearest geometry with deterministic tie-breaker. Never assume display numbering corresponds to physical position.
- Respect updated working area, DPI, negative virtual desktop origins, fixed-size center-versus-clip policy and resizable window size constraints.
- Test disconnected/reconnected monitors, reassigned names, missing paths in legacy data, DPI differences and work-area changes.
- Explain when window geometry is unavoidably not fully visible and any behavior requiring Windows UI integration tests.
