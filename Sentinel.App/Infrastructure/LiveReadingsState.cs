namespace Sentinel.App.Infrastructure;

/// <summary>Global pause state for live readings (like Task Manager's Ctrl pause). When true, timers stop updating.</summary>
public static class LiveReadingsState
{
    private static bool _pausedByUser;
    private static bool _pausedByCtrlKey;

    private const int VkControl = 0x11;

    /// <summary>True when live readings are paused (by Pause button or by holding Ctrl).</summary>
    public static bool IsPaused => _pausedByUser || _pausedByCtrlKey;

    /// <summary>Toggle pause when user clicks the Pause/Resume button.</summary>
    public static void TogglePauseByUser()
    {
        _pausedByUser = !_pausedByUser;
        PausedChanged?.Invoke();
    }

    /// <summary>Call from a timer to update Ctrl-key state (hold Ctrl to pause, like Task Manager).</summary>
    public static void UpdateCtrlKeyState()
    {
        try
        {
            var held = (GetKeyState(VkControl) & 0x8000) != 0;
            if (_pausedByCtrlKey != held)
            {
                _pausedByCtrlKey = held;
                PausedChanged?.Invoke();
            }
        }
        catch { /* ignore */ }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int nVk);

    /// <summary>Raised when <see cref="IsPaused"/> changes so ViewModels can start/stop their timers.</summary>
    public static event Action? PausedChanged;
}
