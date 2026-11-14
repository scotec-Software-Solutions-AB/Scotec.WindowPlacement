using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Scotec.WindowPlacement;

namespace WindowPlacement.Tests;

/// <summary>Dependency-free geometry regression tests. Run with dotnet run --project tests/WindowPlacement.Tests.</summary>
[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal static class Program
{
    /// <summary>Executes placement recovery tests and returns nonzero on failure.</summary>
    private static int Main()
    {
        try
        {
            var first = Monitor("DISPLAY1", 0, 0, 1920, 1080, 96, true);
            var second = Monitor("DISPLAY2", 1920, 0, 1920, 1080, 96);
            var third = Monitor("DISPLAY3", 3840, 0, 1920, 1080, 96);
            var saved = Saved(third, new PlacementRect(100, 100, 800, 600));
            var full = WindowPlacementResolver.Resolve(saved, [first, second, third], true);
            Assert(full.Monitor.DeviceName == "DISPLAY3" && full.BoundsPixels.X == 3940, "Same monitor restore");
            var fallback = WindowPlacementResolver.Resolve(saved, [first, second], true);
            Assert(fallback.Monitor.DeviceName == "DISPLAY2", "Nearest available monitor");
            Assert(fallback.BoundsPixels.X == 2020 && fallback.BoundsPixels.Y == 100, "Preserves monitor-relative position");
            var scaled = Monitor("DISPLAY3", 3840, 0, 1920, 1080, 144);
            var dpi = WindowPlacementResolver.Resolve(saved, [first, second, scaled], true);
            Assert(dpi.BoundsPixels.X == 3990 && dpi.BoundsPixels.Y == 150, "DPI-relative position");
            Assert(dpi.BoundsPixels.Width == 1200, "DPI-relative dimensions");
            var small = Monitor("DISPLAY3", 3840, 0, 640, 480, 96);
            var fit = WindowPlacementResolver.Resolve(saved, [small], true);
            Assert(fit.BoundsPixels.Width == 640 && fit.BoundsPixels.Height == 480, "Resizable fit");
            Assert(fit.BoundsPixels.X == 3840 && fit.BoundsPixels.Y == 0, "Fully visible after fit");
            var fixedResult = WindowPlacementResolver.Resolve(saved with { IsResizable = false }, [small], false);
            Assert(fixedResult.BoundsPixels.Width == 800 && fixedResult.BoundsPixels.Height == 600,
                "Fixed window retains size");
            Assert(fixedResult.BoundsPixels.X == 3760 && fixedResult.BoundsPixels.Y == -60,
                "Oversized fixed window centered");
            var taskbar = small with { WorkAreaPixels = new PlacementRect(3840, 40, 640, 440) };
            var shifted = WindowPlacementResolver.Resolve(saved, [taskbar], true);
            Assert(shifted.BoundsPixels.Y == 40, "Respect taskbar work area");
            var normal = Saved(first, new PlacementRect(100, 100, 300, 200));
            var kept = WindowPlacementResolver.Resolve(normal, [first], false);
            Assert(kept.BoundsPixels.X == 100, "Normal fixed-size placement");
            Assert(saved.Monitor.DeviceName == "DISPLAY3", "Original persisted monitor retained");
            // The interface path survives GDI name reassignment: identity takes priority.
            var renamedOriginal = third with { DeviceName = "DISPLAY9", MonitorDevicePath = "MONITOR#A" };
            var identitySaved = Saved(third with { MonitorDevicePath = "MONITOR#A" },
                new PlacementRect(100, 100, 800, 600));
            var stolenName = second with { DeviceName = "DISPLAY3", MonitorDevicePath = "MONITOR#B" };
            var renamed = WindowPlacementResolver.Resolve(identitySaved,
                [first, stolenName, renamedOriginal], true);
            Assert(renamed.Monitor.DeviceName == "DISPLAY9" && !renamed.UsedFallbackMonitor,
                "Interface identity wins over reassigned GDI name");

            // The old GDI name now belongs to a different physical monitor.
            // No matching interface path is present, so choose nearest geometry.
            var conflict = WindowPlacementResolver.Resolve(identitySaved, [first, stolenName], true);
            Assert(conflict.UsedFallbackMonitor && conflict.Monitor.MonitorDevicePath == "MONITOR#B",
                "Conflicting interface path rejects false GDI name match");

            // Old JSON has no MonitorDevicePath. It still uses the GDI name.
            var legacy = WindowPlacementResolver.Resolve(saved, [first, second, third], true);
            Assert(legacy.Monitor.DeviceName == "DISPLAY3" && !legacy.UsedFallbackMonitor,
                "Legacy placements still use GDI name matching");

            // An old interface path may disappear under RDP; geometric recovery still works.
            var noPath = WindowPlacementResolver.Resolve(identitySaved,
                [first, second with { DeviceName = "DISPLAY8" }], true);
            Assert(noPath.Monitor.DeviceName == "DISPLAY8" && noPath.UsedFallbackMonitor,
                "Unavailable interface path falls back to nearest geometry");
            Assert(identitySaved.Monitor.MonitorDevicePath == "MONITOR#A",
                "Fallback never mutates persisted interface identity");
            VerifyThreadDpiContextRestored();
            Console.WriteLine("All placement resolver and DPI context checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    ///     Checks that the native monitor provider changes DPI awareness only temporarily,
    ///     leaving the caller's thread context exactly as it was before the query. The test is
    ///     skipped on non-Windows hosts, where no user32 APIs are available.
    /// </summary>
    private static void VerifyThreadDpiContextRestored()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var original = GetThreadDpiAwarenessContext();
        var displays = new Win32MonitorProvider().GetMonitors();
        Assert(displays.Count != 0, "Windows monitor enumeration");
        var after = GetThreadDpiAwarenessContext();
        Assert(AreDpiAwarenessContextsEqual(original, after), "DPI thread context restored");
    }

    /// <summary>Reads the current thread's native DPI-awareness context for regression checks.</summary>
    [DllImport("user32.dll")]
    private static extern nint GetThreadDpiAwarenessContext();

    /// <summary>
    ///     Compares two DPI-awareness contexts using Win32 semantic equality, which is safer
    ///     than comparing raw pseudo-handle values.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AreDpiAwarenessContextsEqual(nint left, nint right);

    /// <summary>Builds a synthetic monitor with a working area equal to its physical rectangle.</summary>
    private static MonitorSnapshot Monitor(string name, double x, double y, double w, double h,
                                           uint dpi, bool primary = false)
    {
        return new MonitorSnapshot
        {
            DeviceName = name,
            BoundsPixels = new PlacementRect(x, y, w, h),
            WorkAreaPixels = new PlacementRect(x, y, w, h),
            DpiX = dpi, DpiY = dpi, IsPrimary = primary
        };
    }

    /// <summary>Builds a saved monitor-relative DIP placement for testing.</summary>
    private static SavedWindowPlacement Saved(MonitorSnapshot monitor, PlacementRect rect)
    {
        return new SavedWindowPlacement
        {
            Monitor = monitor, BoundsDip = rect, IsResizable = true,
            IsMaximized = false, WindowDpi = monitor.DpiX
        };
    }

    /// <summary>Raises a test failure when a condition is false.</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"FAILED: {message}");
        }
    }
}