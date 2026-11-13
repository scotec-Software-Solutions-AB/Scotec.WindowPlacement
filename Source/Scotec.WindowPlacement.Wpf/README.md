# Scotec.WindowPlacement.Wpf

WPF adapter for `Scotec.WindowPlacement`. It adds one extension method, `ResolvedWindowPlacement.ApplyTo(Window)`, which converts a resolved placement into WPF window properties.

## Usage

```csharp
private static readonly IWindowPlacementService Service = new Win32WindowPlacementService();

public MyDialog()
{
	InitializeComponent();
	SourceInitialized += (_, _) =>
	{
		SavedWindowPlacement? saved = LoadSavedPlacement(); // your persistence
		if (saved is null) return;

		var hwnd = new WindowInteropHelper(this).Handle;
		ResolvedWindowPlacement resolved = Service.GetPlacement(
			saved, hwnd, new WindowSizeConstraints { MinWidthDip = MinWidth, MinHeightDip = MinHeight });
		resolved.ApplyTo(this);
	};
	Closing += (_, _) => Save(Service.GetCurrentPlacement(new WindowInteropHelper(this).Handle));
}
```

## Behavior

- Input is `ResolvedWindowPlacement.BoundsPixels` (absolute physical screen pixels). Output is WPF `Left`, `Top`, `Width` and `Height` in DIPs, converted with the window's `HwndSource.CompositionTarget.TransformFromDevice`.
- Sets `WindowStartupLocation.Manual`, assigns the normal bounds with `WindowState.Normal`, then maximizes if `IsMaximized` is set.

## Requirements and exceptions

- Call after `SourceInitialized` (an HWND must exist), on the window's UI thread, and preferably before the window is shown.
- `PlatformNotSupportedException` outside Windows.
- `ArgumentNullException` for null arguments.
- `ArgumentException` for invalid bounds.
- `InvalidOperationException` if called before `SourceInitialized`.

## Limitations

- The conversion uses the window's current DPI. If the window moves to a monitor with a different DPI, WPF may re-layout afterwards, so the result is best effort in mixed-DPI setups.
- The restored size is a WPF window size, and non-client chrome can cause small differences from the native outer rectangle. Use `IWindowPlacementService.ApplyPlacement` when exact outer bounds matter.
- Mixed-DPI and multi-monitor behavior has not been verified by automated tests and needs manual verification on Windows.
