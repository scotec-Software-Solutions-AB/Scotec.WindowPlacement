using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Scotec.WindowPlacement;

/// <summary>
///     Win32 interoperability declarations used by the window placement implementation.
///     RECT and WINDOWPLACEMENT are native structures; keep their layouts unchanged.
/// </summary>
internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const long WsExToolWindow = 0x00000080L;
    internal const int GwlStyle = -16;
    internal const long WsThickFrame = 0x00040000L;
    internal const uint MonitorDefaultToNearest = 2;
    internal const uint MonitorInfoPrimary = 1;
    internal const uint EddGetDeviceInterfaceName = 0x00000001;
    internal const uint DisplayDeviceActive = 0x00000001;
    internal const int SwShowNormal = 1;
    internal const int SwShowMaximized = 3;
    internal const uint SwpNoZOrder = 0x0004;

    internal const uint SwpNoActivate = 0x0010;

    // Windows 10: negative pseudo-handles are documented DPI_AWARENESS_CONTEXT constants.
    internal static readonly nint DpiAwarenessContextPerMonitorAwareV2 = new(-4);
    internal static readonly nint DpiAwarenessContextPerMonitorAware = new(-3);

    /// <summary>
    ///     Enumerates physical/virtual monitors attached to the specified GDI display device.
    ///     EDD_GET_DEVICE_INTERFACE_NAME requests the monitor interface path in DeviceId.
    ///     False is also the normal end-of-enumeration result; it is not automatically an error.
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice result, uint flags);

    /// <summary>Enumerates the currently attached logical display monitors.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

    /// <summary>Retrieves monitor bounds, usable bounds, and device name.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);

    /// <summary>Finds the monitor nearest the supplied rectangle.</summary>
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromRect(ref Rect rect, uint flags);

    /// <summary>Gets the current caller thread's DPI awareness. Only the thread is affected by a change.</summary>
    [DllImport("user32.dll")]
    internal static extern nint GetThreadDpiAwarenessContext();

    /// <summary>
    ///     Temporarily changes the calling thread DPI-awareness context and returns the prior
    ///     context on success, or zero on failure. It does not change Revit's process DPI awareness.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

    /// <summary>Returns whether the requested DPI-awareness context is supported by Windows.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsValidDpiAwarenessContext(nint dpiContext);

    /// <summary>Returns the HWND's current virtualized or physical screen rectangle depending on DPI context.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

    /// <summary>Reads the current show state and restored rectangle of a top-level HWND.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(nint hwnd, ref WindowPlacementNative placement);

    /// <summary>Gets the DPI Windows reports for a particular HWND.</summary>
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hwnd);

    /// <summary>Gets window styles for native resize detection.</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr64(nint hwnd, int index);

    /// <summary>32-bit variant of GetWindowLongPtr.</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    internal static extern int GetWindowLong32(nint hwnd, int index);

    /// <summary>
    ///     Gets window extended styles, including the tool-window flag used to interpret
    ///     WINDOWPLACEMENT normal position coordinates.
    /// </summary>
    internal static long GetExtendedStyle(nint hwnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64()
            : GetWindowLong32(hwnd, GwlExStyle);
    }

    /// <summary>Resolves GetWindowLongPtr across process architectures.</summary>
    internal static long GetStyle(nint hwnd)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GwlStyle).ToInt64()
            : GetWindowLong32(hwnd, GwlStyle);
    }

    /// <summary>Places a native HWND using the supplied native screen rectangle.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    /// <summary>Restores or maximizes a native top-level window.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int cmd);

    /// <summary>Returns monitor-effective DPI (shcore API available on Windows 8.1+).</summary>
    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>Returns whether the HWND exists.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hwnd);

    /// <summary>Constructs an exception using the last Win32 error code.</summary>
    internal static Win32Exception Error()
    {
        return new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Native pixel point used in Win32 rectangles and placement data.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    /// <summary>Native rectangle containing left/top/right/bottom endpoints.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left, Top, Right, Bottom;

        /// <summary>Converts a native rectangle into origin-and-size form.</summary>
        internal readonly PlacementRect ToPlacementRect()
        {
            return new PlacementRect(Left, Top, Right - Left, Bottom - Top);
        }
    }

    /// <summary>Window placement structure returned by GetWindowPlacement.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPlacementNative
    {
        internal uint Length, Flags, ShowCmd;
        internal Point MinPosition, MaxPosition;
        internal Rect NormalPosition;
    }

    /// <summary>Monitor metadata returned by GetMonitorInfo; string is fixed-width UTF-16.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        internal uint Size;
        internal Rect Monitor, Work;
        internal uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string Device;
    }

    /// <summary>
    ///     DISPLAY_DEVICEW used to obtain the GUID_DEVINTERFACE_MONITOR device-interface
    ///     path associated with a GDI display name. All string fields are fixed-size UTF-16 arrays.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayDevice
    {
        internal uint Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceString;

        internal uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string DeviceKey;
    }

    /// <summary>Callback used by EnumDisplayMonitors.</summary>
    internal delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, nint rect, nint data);
}
