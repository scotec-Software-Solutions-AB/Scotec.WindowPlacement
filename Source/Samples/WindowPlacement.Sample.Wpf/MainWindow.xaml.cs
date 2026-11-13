using System;
using System.Windows;

namespace WindowPlacement.Sample.Wpf;

/// <summary>Demonstrates opening a modal WPF dialog repeatedly, with persisted position and size.</summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the sample's main window and its controls.</summary>
    public MainWindow() => InitializeComponent();

    /// <summary>Opens the dialog and displays a status message when it closes.</summary>
    private void OpenDialog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SampleDialog { Owner = this };
            dialog.ShowDialog();
            Status.Text = $"Saved placement: {SampleDialog.StoragePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Placement error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
