1. **Restores** in its constructor: reads the saved JSON, calls `IWindowPlacementService.GetPlacement` (with the dialog's `MinWidth` and `MinHeight` as `WindowSizeConstraints`) and applies the result with the Avalonia `ApplyTo` extension. The extension sets an initial position and size, then applies the exact native outer bounds after the window has opened and pins `Width`/`Height` to the resulting client size. If the restore fails, the dialog is centered on its owner instead.
2. **Captures** in `Closing`: reads the native window handle with `TryGetPlatformHandle`, calls `GetCurrentPlacement` and writes the JSON through a temporary file and an overwriting move.

Storage location: `%LOCALAPPDATA%\Scotec\WindowPlacementSampleAvalonia\dialog.json`

## Run

```powershell
dotnet run --project Source/Samples/WindowPlacement.Sample.Avalonia
```

## Sample-level behavior (not library behavior)

- Restore and save errors are written to `Debug` output and never prevent the dialog from opening or closing. The library itself does not swallow failures.
- The placement file is not coordinated between multiple running instances.
- Multi-monitor and mixed-DPI results depend on the machine. A 150% / 100% two-monitor setup was verified manually; other setups can only be confirmed by running the sample on them.
- Delete `dialog.json` to start from the XAML default size and position.
