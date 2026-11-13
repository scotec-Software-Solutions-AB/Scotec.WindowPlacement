using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WindowPlacement.Sample.Avalonia;

/// <summary>Main window that repeatedly opens the placement-aware modal dialog.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window and its controls.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog modally and reports where the placement was stored.</summary>
    private async void OpenDialog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await new SampleDialog().ShowDialog(this);
            Status.Text = $"Saved placement: {SampleDialog.StoragePath}";
        }
        catch (Exception ex)
        {
            Status.Text = $"Placement error: {ex.Message}";
        }
    }
}
