using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Scotec.WindowPlacement;
using Scotec.WindowPlacement.Avalonia;

namespace WindowPlacement.Sample.Avalonia;

/// <summary>
///     Non-modal Avalonia dialog that restores its saved placement before it is shown and captures the
///     native placement when it closes. Persistent data is written beneath LocalAppData.
/// </summary>
public partial class SampleDialog : Window
{
    /// <summary>Core service used for capture and resolution (resolution never moves the window).</summary>
    private static readonly IWindowPlacementService PlacementService = new Win32WindowPlacementService();

    /// <summary>Creates the dialog and restores the stored placement, if any.</summary>
    public SampleDialog()
    {
        InitializeComponent();
        RestorePlacement();
        Closing += OnClosing;
    }

    /// <summary>Absolute path to the sample's persisted JSON placement file.</summary>
    public static string StoragePath => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "Scotec", "WindowPlacementSampleAvalonia", "dialog.json");

    /// <summary>
    ///     Resolves the saved placement against current monitors and applies it with the Avalonia
    ///     adapter. The native window does not exist yet, so the adapter sets position in physical
    ///     pixels and size in logical units using the destination monitor's DPI. A corrupt or stale
    ///     file must never prevent the dialog from showing, so failures fall back to CenterOwner
    ///     (sample-level policy, not library behavior).
    /// </summary>
    private void RestorePlacement()
    {
        try
        {
            if (!File.Exists(StoragePath))
            {
                return;
            }

            var saved = JsonSerializer.Deserialize<SavedWindowPlacement>(File.ReadAllText(StoragePath));
            if (saved is null)
            {
                return;
            }

            var resolved = PlacementService.GetPlacement(saved, 0,
                new WindowSizeConstraints { MinWidthDip = MinWidth, MinHeightDip = MinHeight });
            resolved.ApplyTo(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Placement restore failed: {ex}");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    /// <summary>
    ///     Captures the native placement while the HWND still exists and replaces the JSON file via a
    ///     temporary file. Failures are logged only, so the user can always close the dialog.
    /// </summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? 0;
            if (hwnd == 0)
            {
                return;
            }

            var placement = PlacementService.GetCurrentPlacement(hwnd);
            Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
            var tempPath = StoragePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(placement,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, StoragePath, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Placement save failed: {ex}");
        }
    }

    /// <summary>Closes the dialog from its Close button.</summary>
    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
