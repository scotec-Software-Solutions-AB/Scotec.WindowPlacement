namespace Scotec.WindowPlacement;

/// <summary>Represents a rectangle in a coordinate system identified by its consumer. Values are doubles
/// to preserve device-independent positions without rounding during serialization and scaling.</summary>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
/// <param name="Width">Non-negative horizontal extent.</param>
/// <param name="Height">Non-negative vertical extent.</param>
public readonly record struct PlacementRect(double X, double Y, double Width, double Height)
{
    /// <summary>Returns whether the rectangle has finite values and strictly positive dimensions.</summary>
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) &&
                           double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}

/// <summary>Captures a monitor's Windows display name, optional device-interface identity,
/// virtual desktop geometry and scaling at capture time. Geometry is in physical screen pixels.
/// Neither the display name nor the device-interface path is guaranteed to survive every hardware,
/// driver, virtual-machine, or remote-desktop reconfiguration.</summary>
public sealed record MonitorSnapshot
{
    /// <summary>Windows display device name, such as \\.\DISPLAY2. It may change after reconfiguration.</summary>
    public required string DeviceName { get; init; }
    /// <summary>Optional Windows monitor interface path (GUID_DEVINTERFACE_MONITOR) obtained
    /// using EnumDisplayDevices with EDD_GET_DEVICE_INTERFACE_NAME. When available this is
    /// preferred over the GDI display name, which Windows can reassign. Older serialized
    /// placements without this property remain valid and use legacy matching.</summary>
    public string? MonitorDevicePath { get; init; }
    /// <summary>Monitor rectangle in physical virtual-desktop screen pixels.</summary>
    public required PlacementRect BoundsPixels { get; init; }
    /// <summary>Usable monitor rectangle excluding taskbars, in physical screen pixels.</summary>
    public required PlacementRect WorkAreaPixels { get; init; }
    /// <summary>Effective DPI along the horizontal axis. The reference DPI is 96.</summary>
    public required uint DpiX { get; init; }
    /// <summary>Effective DPI along the vertical axis. The reference DPI is 96.</summary>
    public required uint DpiY { get; init; }
    /// <summary>Whether the monitor was primary when the snapshot was taken.</summary>
    public required bool IsPrimary { get; init; }
}

/// <summary>Persistent description of the normal window rectangle, relative to the monitor working
/// area in device-independent pixels (DIPs, 96 units per inch). The original monitor snapshot is
/// intentionally retained when an unavailable monitor is temporarily replaced by a fallback.</summary>
public sealed record SavedWindowPlacement
{
    /// <summary>Monitor associated with the window's normal (non-maximized) position.</summary>
    public required MonitorSnapshot Monitor { get; init; }
    /// <summary>Normal rectangle relative to the saved monitor work-area origin, in DIPs.</summary>
    public required PlacementRect BoundsDip { get; init; }
    /// <summary>Whether the source window had a native sizing frame when captured.</summary>
    public required bool IsResizable { get; init; }
    /// <summary>Whether the window was maximized when captured. Minimized windows are saved as normal.</summary>
    public required bool IsMaximized { get; init; }
    /// <summary>The DPI reported by GetDpiForWindow for the source HWND, which may differ from monitor DPI
    /// when the window isn't per-monitor DPI-aware. This is informational, not used as the scaling basis.</summary>
    public required uint WindowDpi { get; init; }
}

/// <summary>Placement resolved against the currently connected monitors. The rectangle uses physical
/// screen pixels and is suitable for a native positioning API operating in physical-pixel coordinates.
/// A framework adapter is responsible for converting it to its window coordinate system.</summary>
public sealed record ResolvedWindowPlacement
{
    /// <summary>Currently available target monitor.</summary>
    public required MonitorSnapshot Monitor { get; init; }
    /// <summary>Normal outer window bounds in absolute physical virtual-desktop coordinates.</summary>
    public required PlacementRect BoundsPixels { get; init; }
    /// <summary>Whether the caller should maximize the window after restoring normal bounds.</summary>
    public required bool IsMaximized { get; init; }
    /// <summary>Whether a different monitor was selected because the source monitor was missing.</summary>
    public required bool UsedFallbackMonitor { get; init; }
}

/// <summary>Optional constraints in DIPs for a resizable window. These can enforce application-level
/// minimum and maximum sizes that cannot be inferred reliably from a native window style.</summary>
public sealed record WindowSizeConstraints
{
    /// <summary>Minimum allowed width in DIPs; zero means no additional minimum.</summary>
    public double MinWidthDip { get; init; }
    /// <summary>Minimum allowed height in DIPs; zero means no additional minimum.</summary>
    public double MinHeightDip { get; init; }
}
