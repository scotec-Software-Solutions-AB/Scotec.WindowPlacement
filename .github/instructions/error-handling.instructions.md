---
description: Failure handling for native APIs, invalid placement data and persistence.
applyTo: "**/*.cs"
---
# Errors and diagnostics

- Validate native inputs, saved geometry and DPI before arithmetic; guard against zero DPI, NaN/infinity, invalid rectangles and missing monitors.
- Check Win32 API success/error results and avoid continuing with falsely assumed physical coordinates if DPI scope entry fails.
- Restore native thread DPI context and release resources on every exit path. Preserve exception stacks with `throw;`.
- Keep core exceptions visible to callers. Explicitly document predictable fallbacks such as missing monitor; do not hide unrelated interop faults.
- Avoid introducing logging/DI frameworks unless the repository already uses them or the task requires it. Never log private device information without a clear need.
- Sample settings persistence may catch IO errors to avoid blocking dialog closure, but should document that behavior; production callers decide persistence policy.
