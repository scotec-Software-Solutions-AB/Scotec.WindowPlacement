---
description: WPF and Avalonia adapters and framework/native coordinate conversion.
applyTo: "src/Scotec.WindowPlacement.*/**/*.cs"
---
# Framework adapters

- The core result uses physical virtual-desktop pixels. Do not assign those physical dimensions directly to WPF `Left`/`Top`/`Width`/`Height` (DIPs).
- Avalonia uses physical `PixelPoint` for `Window.Position`; `Width` and `Height` are logical units. Respect target monitor scaling and framework startup position/size-to-content.
- In WPF, `HwndSource.CompositionTarget.TransformFromDevice` can describe the window's **current** DPI, which may differ from its destination monitor during a move. Mixed-DPI transitions require tests; do not assume a simple global factor converts all desktop coordinates.
- Framework properties may express client size or outer-window geometry differently. Avoid promising pixel-exact outer bounds from a framework adapter; native `ApplyPlacement` is available where appropriate.
- Call on the framework UI thread after the platform handle exists; restore at a lifecycle point that does not produce a visible jump and will not be overwritten by automatic positioning.
- No framework API call under a temporary native DPI-awareness scope. Ensure platform checks throw on non-Windows.
- Do not add direct framework references to `Scotec.WindowPlacement`.
