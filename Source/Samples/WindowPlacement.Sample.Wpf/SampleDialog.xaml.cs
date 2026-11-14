using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Scotec.WindowPlacement;
using Scotec.WindowPlacement.Wpf;

namespace WindowPlacement.Sample.Wpf;

/// <summary>
///     Modal WPF dialog that restores its saved placement after its HWND is created, and
///     captures native placement during Closing. Persistent data is written beneath LocalAppData.
/// </summary>
public partial class SampleDialog
{
    private static readonly IWindowPlacementService PlacementService = new Win32WindowPlacementService();

    /// <summary>Creates the WPF dialog, subscribing to HWND creation and closing events.</summary>
    public SampleDialog()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    /// <summary>Absolute path to the demonstration's persisted JSON placement file.</summary>
    public static string StoragePath => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "Scotec", "WindowPlacementSample", "dialog.json");

    /// <summary>
    ///     Resolves persisted placement when the HWND has been created but before the dialog
    ///     becomes visible. Invokes the WPF extension for physical-to-WPF coordinate conversion.
    /// </summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (!File.Exists(StoragePath))
        {
            return;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<SavedWindowPlacement>(File.ReadAllText(StoragePath));
            if (saved is null)
            {
                return;
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            var resolved = PlacementService.GetPlacement(saved, hwnd,
                new WindowSizeConstraints { MinWidthDip = MinWidth, MinHeightDip = MinHeight });
            resolved.ApplyTo(this); // WPF adapter (not the Win32 ApplyPlacement method).
        }
        catch (Exception ex)
        {
            // Corrupt/stale placement must never prevent a modal dialog from becoming visible.
            Debug.WriteLine($"Placement restore failed: {ex}");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    /// <summary>
    ///     Captures HWND placement before WPF destroys its window, then atomically replaces
    ///     the JSON file using a temporary file in the same directory. Failure is logged without
    ///     preventing the user from closing the dialog.
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var placement = PlacementService.GetCurrentPlacement(hwnd);
            var folder = Path.GetDirectoryName(StoragePath)!;
            Directory.CreateDirectory(folder);
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
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
