---
description: Native Win32 coordinate systems, DPI-awareness contexts and HWND lifecycle rules.
applyTo: "src/Scotec.WindowPlacement/**/*.cs"
---
# Win32 interop and DPI

- Verify P/Invoke signatures, struct layout, Unicode strings, monitor handles, flags and error propagation against Win32 documentation before modifying interop.
- A `HWND` is not a portable UI abstraction. Validate handles; use the owning UI thread for operations that reposition windows.
- `GetWindowRect` may be DPI-virtualized; DWM extended frame bounds and `WINDOWPLACEMENT` have different semantics. Never interchange screen/workspace coordinates without conversion.
- `GetDpiForWindow` reports the *window awareness context's* DPI, which can differ from effective monitor DPI. Review DPI awareness mode before interpreting it.
- Use the existing scoped thread-DPI-awareness helper to obtain consistent native coordinates. Restore the exact prior context even if a call throws; no `await`, callbacks into WPF/Avalonia, or window creation while switched.
- Changing a DPI context does not itself repaint windows; moving/resizing can invoke synchronous window messages, layout and redraw.
- `EnumDisplayDevicesW` monitor interface paths can change with RDP, VMs, drivers, docks and hardware. Do not assume `\\.\DISPLAYn` is stable.
- Preserve `WINDOWPLACEMENT` normal bounds and window state; account for tool windows, negative virtual-screen coordinates, taskbar work areas and non-client frame dimensions.
- Throw `PlatformNotSupportedException` for public Win32 operations outside Windows, and do not claim Linux/macOS compatibility.
