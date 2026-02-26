using Microsoft.Win32;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Platform.Collectors;

public sealed class StartupCollector : IStartupCollector
{
    private const string RunKeyCu = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyLm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedCu = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedLm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public Task<IReadOnlyList<StartupItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<StartupItem>();
        try
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyCu))
                {
                    if (key != null)
                        AddRunEntries(key, list, "HKCU Run", useCurrentUserApproved: true);
                }
            }
            catch (System.Security.SecurityException) { }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(RunKeyLm);
                if (key != null)
                    AddRunEntries(key, list, "HKLM Run", useCurrentUserApproved: false);
            }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }

            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder))
            {
                foreach (var f in Directory.GetFiles(startupFolder, "*.lnk"))
                    list.Add(new StartupItem { Id = "folder:" + f, Name = Path.GetFileNameWithoutExtension(f), Command = f, Location = "Startup folder", IsEnabled = true });
                var disabledDir = Path.Combine(startupFolder, "Disabled");
                if (Directory.Exists(disabledDir))
                {
                    foreach (var f in Directory.GetFiles(disabledDir, "*.lnk"))
                        list.Add(new StartupItem { Id = "folder:" + f, Name = Path.GetFileNameWithoutExtension(f), Command = f, Location = "Startup folder", IsEnabled = false });
                }
            }
        }
        catch { }
        return Task.FromResult((IReadOnlyList<StartupItem>)list);
    }

    public Task EnableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        if (item.Location.StartsWith("HKCU"))
            RestoreRunEntry(item);
        else if (item.Location.StartsWith("Startup folder") && !string.IsNullOrEmpty(item.Command) && File.Exists(item.Command))
        {
            var disabledPath = item.Command;
            var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(disabledPath)!);
            var fileName = Path.GetFileName(disabledPath);
            if (!string.IsNullOrEmpty(parentDir) && !string.IsNullOrEmpty(fileName))
                File.Move(disabledPath, Path.Combine(parentDir, fileName));
        }
        return Task.CompletedTask;
    }

    public Task DisableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        if (item.Location.StartsWith("HKCU"))
            RemoveRunEntry(item);
        else if (item.Location.StartsWith("Startup folder"))
        {
            var dir = Path.GetDirectoryName(item.Command)!;
            var disabledDir = Path.Combine(dir, "Disabled");
            Directory.CreateDirectory(disabledDir);
            File.Move(item.Command, Path.Combine(disabledDir, Path.GetFileName(item.Command)!));
        }
        return Task.CompletedTask;
    }

    private static void AddRunEntries(RegistryKey key, List<StartupItem> list, string location, bool useCurrentUserApproved)
    {
        const string disabledPrefix = "_Sentinel_Disabled_";
        var approvedKey = useCurrentUserApproved
            ? Registry.CurrentUser.OpenSubKey(StartupApprovedCu)
            : null;
        if (!useCurrentUserApproved)
        {
            try { approvedKey = Registry.LocalMachine.OpenSubKey(StartupApprovedLm); } catch (System.Security.SecurityException) { } catch (UnauthorizedAccessException) { }
        }
        try
        {
            foreach (var valueName in key.GetValueNames())
            {
                var val = key.GetValue(valueName);
                if (val is not string cmd) continue;
                if (valueName.StartsWith(disabledPrefix, StringComparison.Ordinal))
                {
                    var originalName = valueName.Substring(disabledPrefix.Length);
                    list.Add(new StartupItem
                    {
                        Id = location + ":" + originalName,
                        Name = originalName,
                        Command = cmd,
                        Location = location,
                        IsEnabled = false
                    });
                }
                else
                {
                    var isEnabledByWindows = IsStartupApprovedByWindows(approvedKey, valueName);
                    list.Add(new StartupItem
                    {
                        Id = location + ":" + valueName,
                        Name = valueName,
                        Command = cmd,
                        Location = location,
                        IsEnabled = isEnabledByWindows
                    });
                }
            }
        }
        finally
        {
            approvedKey?.Dispose();
        }
    }

    /// <summary>Returns true if Windows considers this startup entry enabled (Task Manager uses StartupApproved\Run binary: first byte 0x03 = disabled).</summary>
    private static bool IsStartupApprovedByWindows(RegistryKey? approvedKey, string valueName)
    {
        if (approvedKey == null) return true;
        try
        {
            var bin = approvedKey.GetValue(valueName) as byte[];
            if (bin == null || bin.Length < 1) return true;
            return bin[0] != 0x03; // 0x03 = disabled by Task Manager / Settings
        }
        catch
        {
            return true;
        }
    }

    private static void RemoveRunEntry(StartupItem item)
    {
        var parts = item.Id.Split(':', 2);
        if (parts.Length != 2) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyCu, writable: true);
            key?.SetValue("_Sentinel_Disabled_" + item.Name, item.Command);
            key?.DeleteValue(item.Name);
        }
        catch { }
    }

    private static void RestoreRunEntry(StartupItem item)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyCu, writable: true);
            var backup = key?.GetValue("_Sentinel_Disabled_" + item.Name);
            if (backup is string cmd)
            {
                key?.SetValue(item.Name, cmd);
                key?.DeleteValue("_Sentinel_Disabled_" + item.Name);
            }
        }
        catch { }
    }
}
