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
///     <para>
///         The scope must be disposed synchronously on the same thread. Do not await, create windows,
///         or call framework layout APIs while it is active. Nested scopes restore their previous contexts
///         in LIFO order. Existing WPF/Avalonia window DPI modes remain owned by their frameworks.
///     </para>
///     <para>
///         Why it exists: in a DPI-unaware or system-aware host, Win32 returns virtualized
///         coordinates for window and monitor rectangles. Entering a per-monitor-aware context for a short
///         time yields real physical pixels without touching the host's own DPI behavior.
///     </para>
///     <para>
///         Usage: <c>using var scope = DpiAwarenessScope.Enter();</c>. If restoring the previous
///         context fails, <see cref="Dispose" /> throws, which can hide an exception that is already
///         propagating from the guarded block; this is intentional so that an inconsistent thread state
///         is never silent.
///     </para>
/// </remarks>
internal sealed class DpiAwarenessScope : IDisposable
{
	/// <summary>The thread's DPI-awareness context before this scope was entered; restored on disposal.</summary>
	private readonly nint _previousContext;

	/// <summary>Managed id of the thread that entered the scope; the scope must be disposed there.</summary>
	private readonly int _threadId;

	/// <summary>Whether the previous context has been restored successfully.</summary>
	private bool _disposed;

	/// <summary>Creates the scope object after the thread context has already been changed.</summary>
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
	/// <remarks>
	///     Disposing more than once is harmless after a successful restore. After a failed restore
	///     the scope is not marked as disposed, so a later call retries the restoration.
	/// </remarks>
	/// <exception cref="InvalidOperationException">The scope is disposed on a different thread than the one that created it.</exception>
	/// <exception cref="Win32Exception">The original context could not be restored.</exception>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// DPI awareness is per thread; restoring it from another thread would change the wrong thread.
		if (_threadId != Environment.CurrentManagedThreadId)
		{
			throw new InvalidOperationException("DPI scope must be disposed on its creating thread.");
		}

		// SetThreadDpiAwarenessContext returns the replaced context, or zero on failure.
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
	/// <exception cref="PlatformNotSupportedException">
	///     The operating system is not Windows, or it supports neither per-monitor DPI-awareness context.
	/// </exception>
	/// <exception cref="Win32Exception">The thread context could not be changed.</exception>
	internal static DpiAwarenessScope Enter()
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("Native DPI-awareness scopes require Windows.");
		}

		// Prefer per-monitor V2 (Windows 10 1703+), which also scales non-client areas, and fall
		// back to V1 when the system does not know V2.
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
