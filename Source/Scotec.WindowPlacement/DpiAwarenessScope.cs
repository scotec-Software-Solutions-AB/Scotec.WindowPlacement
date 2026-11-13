using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Scotec.WindowPlacement;

/// <summary>
///     Temporarily selects a per-monitor DPI-aware coordinate context for native Win32 geometry
///     queries. This scope changes only the executing thread; it never changes the process default,
///     and it neither creates windows nor changes existing windows' DPI-awareness contexts.
/// </summary>
/// <remarks>
///     The scope must be disposed synchronously on the same thread. Do not await, create windows,
///     or call framework layout APIs while it is active. Nested scopes restore their previous contexts
///     in LIFO order. Existing WPF/Avalonia window DPI modes remain owned by their frameworks.
/// </remarks>
internal sealed class DpiAwarenessScope : IDisposable
{
    private readonly nint _previousContext;
    private readonly int _threadId;
    private bool _disposed;

    /// <summary>Changes the current thread's context, saving the previous context for restoration.</summary>
    /// <param name="previousContext">Successful SetThreadDpiAwarenessContext return value.</param>
    private DpiAwarenessScope(nint previousContext)
    {
        _previousContext = previousContext;
        _threadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    ///     Restores the exact original thread DPI-awareness context. Throws if a caller attempts
    ///     to dispose this scope on a different managed thread or if Windows rejects the restoration.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_threadId != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException("DPI scope must be disposed on its creating thread.");
        }

        if (NativeMethods.SetThreadDpiAwarenessContext(_previousContext) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Could not restore the original thread DPI-awareness context.");
        }

        _disposed = true;
    }

    /// <summary>
    ///     Enters a per-monitor-V2 context, or per-monitor-V1 if V2 is unavailable. Fails explicitly
    ///     rather than silently performing potentially virtualized geometry calculations. The caller
    ///     must run on Windows 10 or later and dispose the returned scope on the same thread.
    /// </summary>
    /// <returns>A synchronous scope that restores the original thread context on disposal.</returns>
    internal static DpiAwarenessScope Enter()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Native DPI-awareness scopes require Windows.");
        }

        var preferred = NativeMethods.DpiAwarenessContextPerMonitorAwareV2;
        if (!NativeMethods.IsValidDpiAwarenessContext(preferred))
        {
            preferred = NativeMethods.DpiAwarenessContextPerMonitorAware;
        }

        if (!NativeMethods.IsValidDpiAwarenessContext(preferred))
        {
            throw new PlatformNotSupportedException("This Windows version does not support per-monitor DPI awareness.");
        }

        var previous = NativeMethods.SetThreadDpiAwarenessContext(preferred);
        if (previous == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Could not temporarily enable per-monitor DPI awareness on this thread.");
        }

        return new DpiAwarenessScope(previous);
    }
}
