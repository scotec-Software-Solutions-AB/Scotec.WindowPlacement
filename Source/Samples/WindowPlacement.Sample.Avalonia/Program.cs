using Avalonia;

namespace WindowPlacement.Sample.Avalonia;

/// <summary>Entry point of the Avalonia placement sample.</summary>
internal static class Program
{
    /// <summary>Starts the classic desktop lifetime. Windows-only sample.</summary>
    /// <param name="args">Command-line arguments.</param>
    [System.STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Configures Avalonia with the platform backend.</summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect();
    }
}
