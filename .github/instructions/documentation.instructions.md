---
description: Detailed XML documentation and project guidance for window placement APIs.
applyTo: "**/*"
---
# Documentation

- Document interfaces, classes and **every method, including private helpers**, with meaningful XML comments. For API members include appropriate `<summary>`, `<param>`, `<returns>`, `<exception>` and `<remarks>`.
- Explain coordinate units (physical pixels or DIPs), origin (desktop or monitor working area), monitor identity priority, fallback behavior, DPI-awareness scope and threading.
- Describe compatibility of serialized saved placements; keep new optional fields backward compatible where possible.
- Mention Windows-only support and the limitations of native HWND/window geometry and UI adapters; avoid claims of untested platform support.
- Keep README snippets aligned with actual types and signatures. Use valid `<see cref="..."/>` and report XML-doc/compiler warnings if verified.
- Keep XML documentation specific and useful: avoid boilerplate paraphrases or invented guarantees.
