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
///     Non-modal WPF dialog that restores its saved placement after its HWND is created, and
///     captures native placement during Closing or when its owner closes. Persistent data is written beneath LocalAppData.
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
    ///     becomes visible. Invokes the WPF extension, which applies the exact physical bounds natively.
    /// </summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Closing an owner destroys its owned windows natively (no WM_CLOSE), so Closing is not raised on
        // the dialog (for example while minimized). The owner's Closing still runs while the HWND exists.
        if (Owner is { } owner)
        {
            owner.Closing += OnOwnerClosing;
        }

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
            // Corrupt/stale placement must never prevent the dialog from becoming visible.
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
        if (Owner is { } owner)
        {
            owner.Closing -= OnOwnerClosing;
        }

        SavePlacement();
    }

    /// <summary>Saves the placement when the owner starts closing; the dialog may never get Closing.</summary>
    /// <param name="sender">The owner window.</param>
    /// <param name="e">Closing event data; nothing is saved if the close was cancelled.</param>
    private void OnOwnerClosing(object? sender, CancelEventArgs e)
    {
        if (!e.Cancel)
        {
            SavePlacement();
        }
    }

    /// <summary>
    ///     Captures the native placement of the existing HWND and atomically writes it as JSON. Sample-level
    ///     best-effort persistence: IO and native failures are logged and never block closing.
    /// </summary>
    private void SavePlacement()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == 0)
            {
                return;
            }

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
