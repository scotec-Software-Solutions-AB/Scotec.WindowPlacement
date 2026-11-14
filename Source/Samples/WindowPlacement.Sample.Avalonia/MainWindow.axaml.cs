using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WindowPlacement.Sample.Avalonia;

/// <summary>Main window that repeatedly opens the placement-aware non-modal dialog.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the window and its controls.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog non-modally and reports where its placement will be stored.</summary>
    private void OpenDialog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            new SampleDialog().Show(this);
            Status.Text = $"Placement is saved to: {SampleDialog.StoragePath}";
        }
        catch (Exception ex)
        {
            Status.Text = $"Placement error: {ex.Message}";
        }
    }
}
