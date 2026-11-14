# WindowPlacement.Sample.Wpf

WPF demo application (`net8.0-windows`, `WinExe`) for `Scotec.WindowPlacement` and `Scotec.WindowPlacement.Wpf`. It is a demonstration, not a library.

## What it shows

`MainWindow` opens a non-modal, resizable `SampleDialog` with `Show`. The dialog is owned by the main window. Closing the owner destroys an owned (for example minimized) dialog without raising its `Closing` event, so the dialog also saves its placement from the owner's `Closing` event. The dialog:

1. **Restores** in `SourceInitialized`: reads the saved JSON, calls `IWindowPlacementService.GetPlacement` (with the dialog's `MinWidth` and `MinHeight` as `WindowSizeConstraints`) and applies the result with the WPF `ApplyTo` extension. If the restore fails, the dialog is centered on its owner instead.
2. **Captures** in `Closing`: calls `GetCurrentPlacement` on the dialog's HWND and writes the JSON through a `.tmp` file followed by an overwriting move.

Storage location: `%LOCALAPPDATA%\Scotec\WindowPlacementSample\dialog.json`

## Run

On Windows with the .NET 8 Desktop Runtime or SDK, from the repository root:

```
dotnet run --project Source/Samples/WindowPlacement.Sample.Wpf
```

## Things to try manually

- Move or resize the dialog, close it and reopen it.
- Close it while maximized and reopen it.
- Reopen after disconnecting a monitor, changing display scaling, or moving the taskbar.

## Sample-level behavior (not library behavior)

- Persistence errors are logged with `Debug.WriteLine` and deliberately suppressed so the dialog can always open and close. The library itself does not swallow failures.
- It saves only on `Closing`, does not react to monitor changes while the dialog is open, and does not coordinate concurrent writes from multiple instances.
- Multi-monitor and mixed-DPI results depend on the machine and can only be confirmed by running the sample on such a setup.
