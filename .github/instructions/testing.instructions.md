---
description: Resolver unit tests and Windows integration regression coverage.
applyTo: "tests/**/*.cs"
---
# Tests

- Inspect `tests/WindowPlacement.Tests` before editing; use its actual dependency-free test harness and conventions rather than inventing a test framework.
- Pure resolver tests: unchanged monitor; path match after name change; reject reused name with conflicting path; legacy saved data without a path; geometric nearest and primary tie; negative monitor coordinates; disconnected monitor; changed work area/taskbar; changed DPI; fixed versus resizable windows; max state; impossible-to-fit sizes; invalid inputs.
- Native Windows tests: DPI awareness scope restores thread context on both success and exception; monitor enumeration; HWND state/normal bounds; no process DPI-awareness change.
- Framework integration checks (manual if necessary): WPF and Avalonia windows on 100%/150% displays, display disconnect/reconnect, VM/RDP screen changes, custom chrome, modal dialog visibility and resize constraints.
- Pure geometry tests do not establish correct API behavior in live mixed-DPI setups. Report exact commands/results and untested Windows scenarios.
