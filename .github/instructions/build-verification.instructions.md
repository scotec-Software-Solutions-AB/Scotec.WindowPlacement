---
description: Build, test, compatibility and package verification workflow.
applyTo: "**/*"
---
# Build and verification

- Inspect `WindowPlacementSolution.sln`, `Directory.Build.props`, and changed `.csproj` files before choosing commands.
- Expected projects: core, WPF adapter, Avalonia adapter, WPF sample and `tests/WindowPlacement.Tests`; verify rather than assuming the set is unchanged.
- Expected TFM is `net8.0-windows`; `EnableWindowsTargeting` may permit cross-host compilation, not cross-host execution.
- On a supported Windows environment, attempt `dotnet build WindowPlacementSolution.sln` and `dotnet run --project tests/WindowPlacement.Tests` if appropriate. WPF sample needs Windows desktop runtime; Avalonia requires restored package dependencies.
- Verify public API/serialization compatibility, optional monitor path, platform checks and XML documentation with builds where possible.
- Distinguish environmental limitations from failures; never claim success without actual execution. Summarize untested multi-monitor/DPI and host integration scenarios.
