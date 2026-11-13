# Scotec.WindowPlacement.Wpf

which applies a resolved placement to a WPF window by setting its exact physical outer bounds natively.

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

- Input is `ResolvedWindowPlacement.BoundsPixels` (absolute physical screen pixels). No DIP conversion is done: the window's current `HwndSource` DPI can differ from the destination monitor's DPI (for example a dialog created on a 150% monitor and restored to a 100% monitor), which would produce a wrong size.
- Sets `WindowStartupLocation.Manual` and `SizeToContent.Manual`, then calls `IWindowPlacementService.ApplyPlacement` on the window handle. That restores the normal bounds with `SetWindowPos`, re-applies them once if the window DPI changed during the move, and maximizes if `IsMaximized` is set.

## Requirements and exceptions

- Call after `SourceInitialized` (an HWND must exist), on the window's UI thread, and preferably before the window is shown.
- `PlatformNotSupportedException` outside Windows.
- `ArgumentNullException` for null arguments.
- `ArgumentException` for invalid bounds.
- `InvalidOperationException` if called before `SourceInitialized`.

## Limitations

- WPF `Left`, `Top`, `Width` and `Height` are updated by WPF in response to the native move, so they may briefly lag behind the native bounds.
- The native call runs synchronously and can deliver window messages (such as `WM_DPICHANGED`) while it executes; call it on the window's UI thread.
- A 150% / 100% two-monitor setup was verified manually; mixed-DPI and multi-monitor behavior is not covered by automated tests.
