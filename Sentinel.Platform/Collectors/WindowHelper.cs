using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Sentinel.Platform.Collectors;

/// <summary>Finds process IDs that have a window on the taskbar (like Task Manager's "Apps" list).</summary>
internal static class WindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int GW_OWNER = 4;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_APPWINDOW = 0x00040000;

    private static readonly List<int> _pids = new();

    public static IReadOnlySet<int> GetProcessIdsWithVisibleWindows()
    {
        _pids.Clear();
        EnumWindows(Callback, IntPtr.Zero);
        return new HashSet<int>(_pids);
    }

    private static bool Callback(IntPtr hwnd, IntPtr lParam)
    {
        if (!IsWindowVisible(hwnd)) return true;
        if (GetWindowThreadProcessId(hwnd, out uint pid) == 0 || pid == 0) return true;

        // Only windows that get a taskbar button (like Task Manager "Apps"):
        var exStyle = (uint)(int)GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return true;

        // Only main app windows (no owner) = what appears as one button per app on the taskbar (Spotify, browser, Discord, OBS, etc.)
        var owner = GetWindow(hwnd, GW_OWNER);
        if (owner != IntPtr.Zero)
            return true;

        // Must have no parent (top-level window)
        if (GetParent(hwnd) != IntPtr.Zero)
            return true;

        // Must have a visible caption/title (filters out most background/system windows)
        if (GetWindowTextLength(hwnd) == 0)
            return true;

        _pids.Add((int)pid);
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
