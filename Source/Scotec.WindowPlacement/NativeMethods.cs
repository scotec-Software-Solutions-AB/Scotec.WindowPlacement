using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Scotec.WindowPlacement;

/// <summary>
///     Win32 interoperability declarations used by the window placement implementation.
///     This is the only place where native signatures live; geometry decisions are made elsewhere.
/// </summary>
/// <remarks>
///     <para>
///         <b>Coordinates.</b> Unless noted otherwise, rectangles are in screen coordinates of the
///         virtual desktop and are expressed in physical pixels when the calling thread is per-monitor
///         DPI-aware (see <see cref="DpiAwarenessScope" />). In a DPI-unaware or system-aware thread
///         context Windows returns virtualized values instead. The virtual desktop origin is the
///         top-left of the primary monitor, so monitors to its left or above have negative coordinates.
///     </para>
///     <para>
///         <b>Layouts.</b> RECT, POINT, WINDOWPLACEMENT, MONITORINFOEXW and DISPLAY_DEVICEW are
///         native structures; keep their field order and types unchanged. They contain no pointer-sized
///         fields, so their layout is identical in 32-bit and 64-bit processes.
///     </para>
///     <para>
///         <b>Threading.</b> The functions operate on the calling thread's DPI-awareness context and,
///         for window functions, must be called on the thread that owns the window.
///     </para>
/// </remarks>
internal static class NativeMethods
{
	/// <summary>GWL_EXSTYLE: index of the extended window style for <c>GetWindowLong(Ptr)</c>.</summary>
	internal const int GwlExStyle = -20;

	/// <summary>WS_EX_TOOLWINDOW: tool window. Its <c>WINDOWPLACEMENT</c> rectangle is in screen coordinates, not workspace coordinates.</summary>
	internal const long WsExToolWindow = 0x00000080L;

	/// <summary>GWL_STYLE: index of the window style for <c>GetWindowLong(Ptr)</c>.</summary>
	internal const int GwlStyle = -16;

	/// <summary>WS_THICKFRAME: window has a sizing border. Used as the heuristic for "resizable".</summary>
	internal const long WsThickFrame = 0x00040000L;

	/// <summary>MONITOR_DEFAULTTONEAREST: <c>MonitorFromRect</c> returns the nearest monitor when there is no intersection.</summary>
	internal const uint MonitorDefaultToNearest = 2;

	/// <summary>MONITORINFOF_PRIMARY: flag in <see cref="MonitorInfoEx.Flags" /> marking the primary monitor.</summary>
	internal const uint MonitorInfoPrimary = 1;

	/// <summary>EDD_GET_DEVICE_INTERFACE_NAME: makes <c>EnumDisplayDevices</c> return the monitor interface path in <see cref="DisplayDevice.DeviceId" />.</summary>
	internal const uint EddGetDeviceInterfaceName = 0x00000001;

	/// <summary>DISPLAY_DEVICE_ACTIVE: flag in <see cref="DisplayDevice.StateFlags" /> for an attached, active display device.</summary>
	internal const uint DisplayDeviceActive = 0x00000001;

	/// <summary>SW_SHOWNORMAL (1): <c>WINDOWPLACEMENT.showCmd</c> / <c>ShowWindow</c> value for a window in its normal (restored) state.</summary>
	internal const int SwShowNormal = 1;

	/// <summary>SW_SHOWMAXIMIZED (3): <c>WINDOWPLACEMENT.showCmd</c> / <c>ShowWindow</c> value for a maximized window.</summary>
	internal const int SwShowMaximized = 3;

	/// <summary>SWP_NOZORDER: <c>SetWindowPos</c> keeps the current Z order and ignores the insert-after handle.</summary>
	internal const uint SwpNoZOrder = 0x0004;

	/// <summary>SWP_NOACTIVATE: <c>SetWindowPos</c> does not activate the window.</summary>
	internal const uint SwpNoActivate = 0x0010;

	/// <summary>
	///     DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 pseudo-handle. Windows 10 (1703) and later.
	///     DPI_AWARENESS_CONTEXT values are documented negative pseudo-handles, not real handles.
	/// </summary>
	internal static readonly nint DpiAwarenessContextPerMonitorAwareV2 = new(-4);

	/// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE pseudo-handle (per-monitor V1), used when V2 is unavailable.</summary>
	internal static readonly nint DpiAwarenessContextPerMonitorAware = new(-3);

	/// <summary>
	///     Enumerates the display devices (monitors) attached to the specified GDI display adapter name.
	///     EDD_GET_DEVICE_INTERFACE_NAME requests the monitor interface path in
	///     <see cref="DisplayDevice.DeviceId" />. A false result is also the normal end-of-enumeration
	///     signal, so it is not automatically an error.
	/// </summary>
	/// <param name="device">GDI display name such as <c>\\.\DISPLAY1</c>, or null for adapters.</param>
	/// <param name="index">Zero-based index of the device to query.</param>
	/// <param name="result">Receives the device; <see cref="DisplayDevice.Size" /> must be initialized first.</param>
	/// <param name="flags">Query flags such as <see cref="EddGetDeviceInterfaceName" />.</param>
	/// <returns>True if a device exists at <paramref name="index" />.</returns>
	[DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice result, uint flags);

	/// <summary>
	///     Enumerates the currently attached logical display monitors, calling
	///     <paramref name="callback" /> synchronously on the calling thread for each one.
	/// </summary>
	/// <param name="hdc">Device context to restrict enumeration to, or zero for the whole virtual screen.</param>
	/// <param name="clip">Optional clip rectangle pointer, or zero for none.</param>
	/// <param name="callback">Called per monitor; return true to continue.</param>
	/// <param name="data">Opaque value passed to the callback (unused by this library).</param>
	/// <returns>True on success.</returns>
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

	/// <summary>
	///     Retrieves monitor bounds, work area (excluding taskbars) and GDI device name.
	///     Rectangles are in physical virtual-desktop pixels for a per-monitor-aware thread.
	/// </summary>
	/// <param name="monitor">HMONITOR from <see cref="EnumDisplayMonitors" />.</param>
	/// <param name="info">Receives the data; <see cref="MonitorInfoEx.Size" /> must be initialized first.</param>
	/// <returns>True on success.</returns>
	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetMonitorInfoW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);

	/// <summary>Finds the monitor that contains the largest part of a window, or the nearest one.</summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="flags">One of the MONITOR_DEFAULTTO* constants, for example <see cref="MonitorDefaultToNearest" />.</param>
	/// <returns>An HMONITOR, or zero if none matches and no default flag applies.</returns>
	[DllImport("user32.dll")]
	internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
	/// <summary>Finds the monitor nearest the supplied rectangle. Currently not used by the library.</summary>
	/// <param name="rect">Rectangle in virtual-desktop screen coordinates.</param>
	/// <param name="flags">One of the MONITOR_DEFAULTTO* constants, for example <see cref="MonitorDefaultToNearest" />.</param>
	/// <returns>An HMONITOR, or zero if none matches and no default flag applies.</returns>
	[DllImport("user32.dll")]
	internal static extern nint MonitorFromRect(ref Rect rect, uint flags);

	/// <summary>Gets the calling thread's current DPI-awareness context. Only the calling thread is concerned.</summary>
	/// <returns>A DPI_AWARENESS_CONTEXT pseudo-handle.</returns>
	[DllImport("user32.dll")]
	internal static extern nint GetThreadDpiAwarenessContext();

	/// <summary>
	///     Changes the calling thread's DPI-awareness context and returns the previous one.
	///     It never changes the process DPI awareness or the awareness of existing windows,
	///     which makes it safe to use temporarily inside a host application.
	/// </summary>
	/// <param name="dpiContext">The context to switch to.</param>
	/// <returns>The previous context on success, or zero on failure (see the last Win32 error).</returns>
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

	/// <summary>Returns whether the requested DPI-awareness context is supported by this version of Windows.</summary>
	/// <param name="dpiContext">The context to check.</param>
	/// <returns>True if the context is valid on this system.</returns>
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsValidDpiAwarenessContext(nint dpiContext);

	/// <summary>
	///     Gets the outer window rectangle (including non-client frame, and for DWM-composed windows
	///     including the invisible resize border) in virtual-desktop screen coordinates.
	///     In a DPI-unaware thread context the values are virtualized, not physical.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="rect">Receives the rectangle.</param>
	/// <returns>True on success.</returns>
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

	/// <summary>Reads the show state and the restored (normal) rectangle of a top-level window.</summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="placement">Receives the data; <see cref="WindowPlacementNative.Length" /> must be initialized first.</param>
	/// <returns>True on success.</returns>
	/// <remarks>
	///     <see cref="WindowPlacementNative.NormalPosition" /> is in workspace coordinates (relative to
	///     the work area, offset by taskbars) for ordinary windows, and in screen coordinates for
	///     WS_EX_TOOLWINDOW windows.
	/// </remarks>
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetWindowPlacement(nint hwnd, ref WindowPlacementNative placement);

	/// <summary>
	///     Gets the DPI Windows reports for a window. The value follows the window's own DPI awareness:
	///     a system-aware window reports the system DPI even when it is on a monitor with a different DPI.
	///     Therefore this value is informational and is not the basis for monitor-relative scaling.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <returns>The DPI, or zero for an invalid window.</returns>
	[DllImport("user32.dll")]
	internal static extern uint GetDpiForWindow(nint hwnd);

	/// <summary>Reads a window long value in a 64-bit process, using the pointer-sized API (<c>GetWindowLongPtrW</c>).</summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="index">One of the GWL_* indexes.</param>
	/// <returns>The value, or zero on failure.</returns>
	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	internal static extern nint GetWindowLongPtr64(nint hwnd, int index);

	/// <summary>
	///     Reads a window long value in a 32-bit process. <c>GetWindowLongPtrW</c> is only a macro on
	///     32-bit Windows and is not exported, so <c>GetWindowLongW</c> is used there.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="index">One of the GWL_* indexes.</param>
	/// <returns>The value, or zero on failure.</returns>
	[DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
	internal static extern int GetWindowLong32(nint hwnd, int index);

	/// <summary>
	///     Gets the extended window styles. The flag of interest is WS_EX_TOOLWINDOW, which changes how
	///     the <c>WINDOWPLACEMENT</c> normal rectangle must be interpreted.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <returns>The extended style bits.</returns>
	internal static long GetExtendedStyle(nint hwnd)
	{
		return IntPtr.Size == 8
			? GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64()
			: GetWindowLong32(hwnd, GwlExStyle);
	}

	/// <summary>
	///     Gets the window styles, choosing the correct <c>GetWindowLong(Ptr)</c> variant for the
	///     process architecture. Only low-order flag bits such as WS_THICKFRAME are tested by callers,
	///     so sign extension of a 32-bit result does not matter.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <returns>The style bits.</returns>
	internal static long GetStyle(nint hwnd)
	{
		return IntPtr.Size == 8
			? GetWindowLongPtr64(hwnd, GwlStyle).ToInt64()
			: GetWindowLong32(hwnd, GwlStyle);
	}

	/// <summary>
	///     Moves and sizes a window. The rectangle is the outer window rectangle in virtual-desktop
	///     screen coordinates. Can send window messages (for example WM_DPICHANGED) synchronously
	///     to the window procedure before returning.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="after">Insert-after handle; ignored with <see cref="SwpNoZOrder" />.</param>
	/// <param name="x">Left edge in physical screen pixels.</param>
	/// <param name="y">Top edge in physical screen pixels.</param>
	/// <param name="width">Outer width in physical pixels.</param>
	/// <param name="height">Outer height in physical pixels.</param>
	/// <param name="flags">Combination of SWP_* flags.</param>
	/// <returns>True on success.</returns>
	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

	/// <summary>
	///     Changes the show state of a window (restore or maximize). Can send window messages
	///     synchronously. The return value is the previous visibility, not a success flag.
	/// </summary>
	/// <param name="hwnd">Window handle.</param>
	/// <param name="cmd">One of the SW_* commands.</param>
	/// <returns>True if the window was previously visible.</returns>
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool ShowWindow(nint hwnd, int cmd);

	/// <summary>
	///     Returns the effective DPI of a monitor (shcore, Windows 8.1 and later). The value is only
	///     meaningful for MDT_EFFECTIVE_DPI (0) when the calling thread is per-monitor DPI-aware.
	/// </summary>
	/// <param name="monitor">HMONITOR.</param>
	/// <param name="dpiType">The MONITOR_DPI_TYPE; 0 is effective DPI.</param>
	/// <param name="dpiX">Receives the horizontal DPI.</param>
	/// <param name="dpiY">Receives the vertical DPI.</param>
	/// <returns>An HRESULT; a negative value is a failure.</returns>
	[DllImport("shcore.dll")]
	internal static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

	/// <summary>Returns whether the window has the WS_VISIBLE style (it is not hidden).</summary>
	/// <param name="hwnd">Window handle.</param>
	/// <returns>True if the window and all of its parents are visible.</returns>
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsWindowVisible(nint hwnd);

	/// <summary>Returns whether the handle identifies an existing window.</summary>
	/// <param name="hwnd">Window handle.</param>
	/// <returns>True if the window exists.</returns>
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool IsWindow(nint hwnd);

	/// <summary>
	///     Creates an exception from the last Win32 error. Call it immediately after the failing
	///     native call that was declared with <c>SetLastError = true</c>.
	/// </summary>
	/// <returns>A <see cref="Win32Exception" /> carrying the error code and the system message.</returns>
	internal static Win32Exception Error()
	{
		return new Win32Exception(Marshal.GetLastWin32Error());
	}

	/// <summary>Native POINT: a position in pixels.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct Point
	{
		/// <summary>Horizontal coordinate.</summary>
		internal int X;

		/// <summary>Vertical coordinate.</summary>
		internal int Y;
	}

	/// <summary>Native RECT: left/top/right/bottom edges. Right and bottom are exclusive.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct Rect
	{
		/// <summary>Left edge.</summary>
		internal int Left;

		/// <summary>Top edge.</summary>
		internal int Top;

		/// <summary>Right edge (exclusive).</summary>
		internal int Right;

		/// <summary>Bottom edge (exclusive).</summary>
		internal int Bottom;

		/// <summary>Converts the edge-based rectangle into origin-and-size form, keeping the same coordinate system.</summary>
		/// <returns>A rectangle with X = Left, Y = Top, Width = Right - Left and Height = Bottom - Top.</returns>
		internal readonly PlacementRect ToPlacementRect()
		{
			return new PlacementRect(Left, Top, Right - Left, Bottom - Top);
		}
	}

	/// <summary>Native WINDOWPLACEMENT returned by <see cref="GetWindowPlacement" />.</summary>
	[StructLayout(LayoutKind.Sequential)]
	internal struct WindowPlacementNative
	{
		/// <summary>Size of this structure in bytes; must be set before the call.</summary>
		internal uint Length;

		/// <summary>WPF_* flags.</summary>
		internal uint Flags;

		/// <summary>Current show command (SW_SHOWNORMAL, SW_SHOWMINIMIZED, SW_SHOWMAXIMIZED).</summary>
		internal uint ShowCmd;

		/// <summary>Position of the minimized window; not used by this library.</summary>
		internal Point MinPosition;

		/// <summary>Position of the maximized window; not used by this library.</summary>
		internal Point MaxPosition;

		/// <summary>
		///     Restored rectangle. Workspace coordinates (relative to the work area) for ordinary
		///     windows, screen coordinates for WS_EX_TOOLWINDOW windows.
		/// </summary>
		internal Rect NormalPosition;
	}

	/// <summary>Native MONITORINFOEXW returned by <see cref="GetMonitorInfo" />.</summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct MonitorInfoEx
	{
		/// <summary>Size of this structure in bytes; must be set before the call.</summary>
		internal uint Size;

		/// <summary>Monitor rectangle in virtual-desktop screen coordinates.</summary>
		internal Rect Monitor;

		/// <summary>Work area (monitor minus taskbars) in virtual-desktop screen coordinates.</summary>
		internal Rect Work;

		/// <summary>MONITORINFOF_* flags, for example <see cref="MonitorInfoPrimary" />.</summary>
		internal uint Flags;

		/// <summary>GDI display device name such as <c>\\.\DISPLAY1</c>, as a fixed 32-character UTF-16 buffer.</summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		internal string Device;
	}

	/// <summary>
	///     Native DISPLAY_DEVICEW used to obtain the GUID_DEVINTERFACE_MONITOR device-interface
	///     path associated with a GDI display name. All string fields are fixed-size UTF-16 arrays.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct DisplayDevice
	{
		/// <summary>Size of this structure in bytes; must be set before the call.</summary>
		internal uint Size;

		/// <summary>Device name (32 characters).</summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		internal string DeviceName;

		/// <summary>Human-readable device description (128 characters).</summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		internal string DeviceString;

		/// <summary>DISPLAY_DEVICE_* state flags, for example <see cref="DisplayDeviceActive" />.</summary>
		internal uint StateFlags;

		/// <summary>With <see cref="EddGetDeviceInterfaceName" />: the monitor interface path (128 characters).</summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		internal string DeviceId;

		/// <summary>Registry key of the device (128 characters); not used by this library.</summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		internal string DeviceKey;
	}

	/// <summary>Native MONITORENUMPROC callback used by <see cref="EnumDisplayMonitors" />.</summary>
	/// <param name="hMonitor">The monitor handle.</param>
	/// <param name="hdcMonitor">Device context of the monitor; unused here.</param>
	/// <param name="rect">Pointer to the monitor intersection RECT; unused here.</param>
	/// <param name="data">The value passed to <see cref="EnumDisplayMonitors" />.</param>
	/// <returns>True to continue the enumeration.</returns>
	internal delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, nint rect, nint data);
}
