namespace Sentinel.Core.Models;

/// <summary>Classify process as Windows (system) vs background (user/app).</summary>
public static class ProcessClassification
{
    private static readonly HashSet<string> WindowsProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "csrss", "wininit", "services", "lsass", "winlogon", "fontdrvhost", "dwm",
        "explorer", "sihost", "taskhostw", "ShellHost", "RuntimeBroker", "SearchHost", "StartMenuExperienceHost",
        "SystemSettings", "SecurityHealthService", "SecurityHealthHost", "ctfmon", "conhost",
        "smss", "csrss", "winlogon", "services", "lsass", "svchost", "MsMpEng", "NisSrv",
        "WmiPrvSE", "dllhost", "splwow64", "SearchIndexer", "SearchProtocolHost", "SearchFilterHost",
        "audiodg", "fontdrvhost", "LockApp", "TextInputHost", "ApplicationFrameHost", "System"
    };

    public static bool IsWindowsProcess(ProcessInfo p)
    {
        if (WindowsProcessNames.Contains(p.Name))
            return true;
        var path = p.Path ?? "";
        return path.Contains("\\Windows\\System32\\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Windows\\SysWOW64\\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("\\Program Files\\Windows ", StringComparison.OrdinalIgnoreCase);
    }
}
