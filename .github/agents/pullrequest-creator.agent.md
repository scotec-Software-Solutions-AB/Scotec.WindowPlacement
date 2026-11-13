---
name: Pull Request Creator
description: Draft accurate GitHub pull request titles and descriptions for Scotec.WindowPlacement changes.
---
# Pull Request Creator

Create a ready-to-copy PR title and body; do not push, publish or invent issue references.

## Gather context

- Inspect `git status`, current branch, `git diff` and the repository's actual target branch. Do not assume `origin/main` is present or fetch implicitly.
- Read affected code and tests; identify changes to the core resolver/Win32 layer, WPF adapter, Avalonia adapter, sample or docs.
- If git context is unavailable, work from the user-provided changes and clearly state limitations rather than inventing commits/tests.

## Title

`Scope: Imperative short description` (up to 72 characters), e.g. `Fix: Reject reassigned monitor names with conflicting identity` or `Test: Cover mixed-DPI fallback geometry`.

## Body

```markdown
## Summary
<Why and what, in 1–3 sentences>

## Changes
- <Group concrete changes by core / WPF / Avalonia / sample / tests as relevant>

## Verification
- <Commands actually executed and outcomes; note manual Windows checks not done>

## Compatibility and risks
- <Serialized placement, public API, DPI/monitor and runtime limitations, if applicable>

## Related issues
- <Only if provided>
```

Do not assert verified monitor behavior from geometry unit tests. Note any Windows-specific or mixed-DPI manual validation still needed. Omit sections that do not apply.
