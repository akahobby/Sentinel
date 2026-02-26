using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using Sentinel.Core.Interfaces;
using System.ServiceProcess;

namespace Sentinel.Platform.Collectors;

/// <summary>Built-in ZeroTrace engine: discovers installed apps and residual targets, executes cleanup.</summary>
public static class ZeroTraceEngine
{
    private static readonly string[] ProtectedPublishers = { "microsoft", "google", "nvidia", "intel", "amd", "valve", "adobe", "apple", "mozilla" };

    public static string NormalizeName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant();
    }

    public static bool IsProtectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        try
        {
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var hardBlocks = new[] { Path.GetPathRoot(full) ?? "C:\\", windir, Path.Combine(windir, "System32"), Path.Combine(windir, "SysWOW64"), programFiles, programFilesX86 };
            foreach (var b in hardBlocks)
            {
                if (string.IsNullOrEmpty(b)) continue;
                try
                {
                    var bFull = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(full, bFull, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { }
            }
        }
        catch { return true; }
        return false;
    }

    public static bool IsSaneInstallLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            if (!Directory.Exists(full)) return false;
            return !IsProtectedPath(full);
        }
        catch { return false; }
    }

    private static long GetDirectorySizeBytes(string path, int timeoutMs = 15000)
    {
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        var start = Environment.TickCount64;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (Environment.TickCount64 - start > timeoutMs) break;
                try { total += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim().Replace('/', Path.DirectorySeparatorChar);
        }
        catch { return null; }
    }

    private static void AddSteamApps(List<ZeroTraceApp> list, HashSet<string> seen)
    {
        var steamPath = GetSteamPath();
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath)) return;
        var steamApps = Path.Combine(steamPath, "steamapps");
        if (!Directory.Exists(steamApps)) return;
        var commonPath = Path.Combine(steamApps, "common");
        var manifests = Directory.EnumerateFiles(steamApps, "appmanifest_*.acf").ToList();
        foreach (var manifestPath in manifests)
        {
            try
            {
                var text = File.ReadAllText(manifestPath);
                var name = GetAcfValue(text, "name");
                var installdir = GetAcfValue(text, "installdir");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installdir)) continue;
                var installLoc = Path.Combine(commonPath, installdir);
                if (!Directory.Exists(installLoc)) continue;
                var key = $"{name}|Steam|{installLoc}";
                if (seen.Contains(key)) continue;
                seen.Add(key);
                list.Add(new ZeroTraceApp(name, "Steam", null, installLoc, null, null, null, "Steam"));
            }
            catch { }
        }
        try
        {
            var libFolders = Path.Combine(steamApps, "libraryfolders.vdf");
            if (!File.Exists(libFolders)) return;
            var vdf = File.ReadAllText(libFolders);
            var pathMatches = System.Text.RegularExpressions.Regex.Matches(vdf, @"""path""\s+""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match m in pathMatches)
            {
                if (m.Success && m.Groups.Count > 1)
                {
                    var libPath = m.Groups[1].Value.Trim().Replace("\\\\", "\\");
                    var libSteamApps = Path.Combine(libPath, "steamapps");
                    var libCommon = Path.Combine(libSteamApps, "common");
                    if (!Directory.Exists(libCommon)) continue;
                    foreach (var manifestPath in Directory.EnumerateFiles(libSteamApps, "appmanifest_*.acf"))
                    {
                        try
                        {
                            var text = File.ReadAllText(manifestPath);
                            var name = GetAcfValue(text, "name");
                            var installdir = GetAcfValue(text, "installdir");
                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installdir)) continue;
                            var installLoc = Path.Combine(libCommon, installdir);
                            if (!Directory.Exists(installLoc)) continue;
                            var key = $"{name}|Steam|{installLoc}";
                            if (seen.Contains(key)) continue;
                            seen.Add(key);
                            list.Add(new ZeroTraceApp(name, "Steam", null, installLoc, null, null, null, "Steam"));
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    private static string? GetAcfValue(string acfContent, string key)
    {
        var pattern = "\"" + key + "\"\\s+\"([^\"]*)\"";
        var m = System.Text.RegularExpressions.Regex.Match(acfContent, pattern);
        return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value.Trim() : null;
    }

    public static Task<IReadOnlyList<ZeroTraceApp>> GetInstalledAppsAsync(CancellationToken ct = default, bool skipSizeCalculation = false)
    {
        return Task.Run(() =>
        {
            var list = new List<ZeroTraceApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? GetStr(object? o) => o is string s ? s.Trim() : null;

            void AddFromKey(RegistryKey root, string subKey)
            {
                try
                {
                    using var uninstall = root.OpenSubKey(subKey);
                    if (uninstall == null) return;
                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var appKey = uninstall.OpenSubKey(name);
                            if (appKey == null) continue;
                            var displayName = GetStr(appKey.GetValue("DisplayName"));
                            if (string.IsNullOrWhiteSpace(displayName)) continue;
                            var key = $"{displayName}|{GetStr(appKey.GetValue("Publisher")) ?? ""}";
                if (seen.Contains(key)) continue;
                seen.Add(key);
                var installLoc = GetStr(appKey.GetValue("InstallLocation"));
                list.Add(new ZeroTraceApp(
                    displayName,
                    GetStr(appKey.GetValue("Publisher")),
                    GetStr(appKey.GetValue("DisplayVersion")),
                    string.IsNullOrWhiteSpace(installLoc) ? null : installLoc,
                    GetStr(appKey.GetValue("UninstallString")),
                    GetStr(appKey.GetValue("QuietUninstallString")),
                    null,
                    "Registry"
                ));
                        }
                        catch { }
                    }
                }
                catch { }
            }

            AddFromKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            AddFromKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            AddFromKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            AddSteamApps(list, seen);

            if (skipSizeCalculation)
            {
                return (IReadOnlyList<ZeroTraceApp>)list.OrderBy(a => a.DisplayName).ToList();
            }

            var result = new ZeroTraceApp[list.Count];
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.For(0, list.Count, opts, i =>
            {
                var app = list[i];
                long? size = null;
                if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
                    size = GetDirectorySizeBytes(app.InstallLocation, 6000);
                result[i] = new ZeroTraceApp(
                    app.DisplayName,
                    app.Publisher,
                    app.DisplayVersion,
                    app.InstallLocation,
                    app.UninstallString,
                    app.QuietUninstallString,
                    size,
                    app.Source
                );
            });

            return (IReadOnlyList<ZeroTraceApp>)result.OrderBy(a => a.DisplayName).ToList();
        }, ct);
    }

    public static List<string> GetCandidateNames(ZeroTraceApp app)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(app.DisplayName)) names.Add(app.DisplayName);
        if (!string.IsNullOrWhiteSpace(app.Publisher)) names.Add(app.Publisher);
        var extra = new List<string>();
        foreach (var n in names)
        {
            foreach (var part in n.Split(new[] { '-', '|', '(', ')', '[', ']', ':', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (!string.IsNullOrWhiteSpace(t)) extra.Add(t);
            }
        }
        var all = names.Concat(extra).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var norm = all.Select(NormalizeName).Where(nn => nn.Length >= 4).Distinct().ToList();
        return norm;
    }

    public static Task<IReadOnlyList<ZeroTraceTarget>> ScanAsync(ZeroTraceApp app, bool fullCleanup, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var candidates = GetCandidateNames(app);
            var targets = new List<ZeroTraceTarget>();
            var standardFolders = new List<string>();
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (Directory.Exists(pf)) standardFolders.Add(pf);
            if (Directory.Exists(pfx86)) standardFolders.Add(pfx86);
            if (Directory.Exists(programData)) standardFolders.Add(programData);

            var userFolders = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (Directory.Exists(localAppData)) userFolders.Add(localAppData);
            if (Directory.Exists(appData)) userFolders.Add(appData);

            var candidateSet = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
            var pubNorm = NormalizeName(app.Publisher);
            var skipAppDataVendor = fullCleanup && !string.IsNullOrEmpty(pubNorm) && ProtectedPublishers.Contains(pubNorm);

            // InstallLocation
            if (IsSaneInstallLocation(app.InstallLocation))
                AddTarget(targets, ZeroTraceTargetKind.Path, app.InstallLocation!, "InstallLocation", ZeroTraceConfidence.High);

            // Exact child folder matches under ProgramFiles/ProgramData
            foreach (var root in standardFolders)
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        var childName = Path.GetFileName(dir);
                        if (NormalizeName(childName).Length >= 4 && candidateSet.Contains(NormalizeName(childName)))
                            AddTarget(targets, ZeroTraceTargetKind.Path, dir, "ExactFolder(Standard)", ZeroTraceConfidence.High);
                    }
                }
                catch { }
            }

            // Start Menu
            var startMenus = new[]
            {
                Path.Combine(programData, "Microsoft", "Windows", "Start Menu", "Programs"),
                Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs")
            };
            foreach (var sm in startMenus)
            {
                if (!Directory.Exists(sm)) continue;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(sm))
                    {
                        var name = Path.GetFileName(dir);
                        if (candidateSet.Contains(NormalizeName(name)))
                            AddTarget(targets, ZeroTraceTargetKind.Path, dir, "StartMenu", ZeroTraceConfidence.Medium);
                    }
                    foreach (var f in Directory.EnumerateFiles(sm, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".url", StringComparison.OrdinalIgnoreCase)))
                    {
                        var baseName = Path.GetFileNameWithoutExtension(f);
                        if (candidateSet.Contains(NormalizeName(baseName)))
                            AddTarget(targets, ZeroTraceTargetKind.Path, f, "StartMenu", ZeroTraceConfidence.Medium);
                    }
                }
                catch { }
            }

            // FullCleanup: AppData exact folder matches (skip protected publishers)
            if (fullCleanup && !skipAppDataVendor)
            {
                foreach (var root in userFolders)
                {
                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(root))
                        {
                            var childName = Path.GetFileName(dir);
                            if (NormalizeName(childName).Length >= 4 && candidateSet.Contains(NormalizeName(childName)))
                                AddTarget(targets, ZeroTraceTargetKind.Path, dir, "ExactFolder(AppData)", ZeroTraceConfidence.Medium);
                        }
                    }
                    catch { }
                }
            }

            // Registry vendor keys - need to open Software and enumerate subkeys
            AddRegistryVendorTargets(targets, candidateSet, Registry.LocalMachine, "SOFTWARE");
            AddRegistryVendorTargets(targets, candidateSet, Registry.LocalMachine, "SOFTWARE\\WOW6432Node");
            AddRegistryVendorTargets(targets, candidateSet, Registry.CurrentUser, "SOFTWARE");

            // Services
            try
            {
                foreach (var svc in ServiceController.GetServices())
                {
                    try
                    {
                        var n1 = NormalizeName(svc.ServiceName);
                        var n2 = NormalizeName(svc.DisplayName);
                        if ((n1.Length >= 4 && candidateSet.Contains(n1)) || (n2.Length >= 4 && candidateSet.Contains(n2)))
                            AddTarget(targets, ZeroTraceTargetKind.Service, svc.ServiceName, "ServiceName/DisplayName", ZeroTraceConfidence.Medium);
                    }
                    finally { svc.Dispose(); }
                }
            }
            catch { }

            // Scheduled tasks (schtasks /query /fo csv /v - parse for TaskName, TaskPath)
            try
            {
                var output = RunProcessOutput("schtasks", "/query /fo csv /v /nh");
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!line.Contains(",\"")) continue;
                    var parts = ParseCsvLine(line);
                    if (parts.Count < 2) continue;
                    var taskName = parts[0].Trim('"').Trim();
                    var taskPath = parts.Count > 1 ? parts[1].Trim('"').Trim().Replace("\\", "\\") : "\\";
                    var n1 = NormalizeName(taskName);
                    var n2 = NormalizeName(taskPath);
                    var match = (n1.Length >= 4 && candidateSet.Contains(n1)) || (n2.Length >= 4 && candidateSet.Any(c => n2.Contains(c)));
                    if (match)
                    {
                        var value = (taskPath.TrimEnd('\\') + "\\" + taskName).Replace("\\\\", "\\");
                        if (!targets.Any(t => t.Kind == ZeroTraceTargetKind.ScheduledTask && string.Equals(t.Value, value, StringComparison.OrdinalIgnoreCase)))
                            AddTarget(targets, ZeroTraceTargetKind.ScheduledTask, value, "TaskName/Path", ZeroTraceConfidence.Medium);
                    }
                }
            }
            catch { }

            // Firewall rules (netsh advfirewall firewall show rule name=all)
            try
            {
                var fwOutput = RunProcessOutput("netsh", "advfirewall firewall show rule name=all");
                var inRule = false;
                var currentName = "";
                foreach (var line in fwOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.TrimStart().StartsWith("Rule Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentName = line.Split(':').Length > 1 ? line.Split(new[] { ':' }, 2)[1].Trim() : "";
                        inRule = true;
                    }
                    else if (inRule && !string.IsNullOrWhiteSpace(currentName))
                    {
                        var nn = NormalizeName(currentName);
                        if (nn.Length >= 4 && candidateSet.Contains(nn))
                        {
                            if (!targets.Any(t => t.Kind == ZeroTraceTargetKind.FirewallRule && string.Equals(t.Value, currentName, StringComparison.OrdinalIgnoreCase)))
                                AddTarget(targets, ZeroTraceTargetKind.FirewallRule, currentName, "FirewallRule", ZeroTraceConfidence.Low);
                        }
                        inRule = false;
                        currentName = "";
                    }
                }
            }
            catch { }

            // Dedupe by Kind|Value
            var uniq = new Dictionary<string, ZeroTraceTarget>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in targets)
            {
                var k = $"{t.Kind}|{t.Value}";
                if (!uniq.ContainsKey(k)) uniq[k] = t;
            }

            // Resolve Exists, Blocked, Meta for each
            var result = new List<ZeroTraceTarget>();
            foreach (var t in uniq.Values)
            {
                var (exists, blocked, meta) = ResolveTarget(t);
                result.Add(t with { Exists = exists, Blocked = blocked, Meta = meta });
            }

            return (IReadOnlyList<ZeroTraceTarget>)result;
        }, ct);
    }

    private static void AddRegistryVendorTargets(List<ZeroTraceTarget> targets, HashSet<string> candidateSet, RegistryKey root, string subPath)
    {
        try
        {
            using var key = root.OpenSubKey(subPath);
            if (key == null) return;
            var prefix = root == Registry.LocalMachine ? "HKLM:\\" : "HKCU:\\";
            foreach (var name in key.GetSubKeyNames())
            {
                var norm = NormalizeName(name);
                if (norm.Length >= 4 && candidateSet.Contains(norm))
                    AddTarget(targets, ZeroTraceTargetKind.RegistryKey, prefix + subPath + "\\" + name, "VendorKey", ZeroTraceConfidence.Medium);
            }
        }
        catch { }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var list = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (!inQuotes && c == ',') { list.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        list.Add(current.ToString());
        return list;
    }

    private static void AddTarget(List<ZeroTraceTarget> list, ZeroTraceTargetKind kind, string value, string source, ZeroTraceConfidence confidence)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        list.Add(new ZeroTraceTarget(kind, value, source, confidence, false, false, null));
    }

    private static (bool exists, bool blocked, string? meta) ResolveTarget(ZeroTraceTarget t)
    {
        switch (t.Kind)
        {
            case ZeroTraceTargetKind.Path:
                var blocked = IsProtectedPath(t.Value);
                if (!Directory.Exists(t.Value) && !File.Exists(t.Value))
                    return (false, blocked, "Missing");
                long files = 0, bytes = 0;
                try
                {
                    if (Directory.Exists(t.Value))
                    {
                        foreach (var f in Directory.EnumerateFiles(t.Value, "*", SearchOption.AllDirectories)) { files++; try { bytes += new FileInfo(f).Length; } catch { } }
                    }
                    else
                    { files = 1; bytes = new FileInfo(t.Value).Length; }
                }
                catch { }
                return (true, blocked, $"Files:{files} Size:{FormatBytes(bytes)}");
            case ZeroTraceTargetKind.RegistryKey:
                var regPath = RegPathToKey(t.Value);
                var existsReg = regPath != null && RegKeyExists(regPath.Value.root, regPath.Value.subKey);
                return (existsReg, false, existsReg ? "Exists" : "Missing");
            case ZeroTraceTargetKind.Service:
                var existsSvc = ServiceExists(t.Value);
                return (existsSvc, false, existsSvc ? "Exists" : "Missing");
            case ZeroTraceTargetKind.ScheduledTask:
                var (path, name) = SplitTaskValue(t.Value);
                var existsTask = ScheduledTaskExists(path, name);
                return (existsTask, false, existsTask ? "Exists" : "Missing");
            case ZeroTraceTargetKind.FirewallRule:
                var existsFw = FirewallRuleExists(t.Value);
                return (existsFw, false, existsFw ? "Exists" : "Missing");
            default:
                return (false, false, null);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:N1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):N1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):N2} GB";
    }

    private static (RegistryKey root, string subKey)? RegPathToKey(string path)
    {
        if (path.StartsWith("HKLM:\\", StringComparison.OrdinalIgnoreCase))
            return (Registry.LocalMachine, path.Substring(6).TrimStart('\\').Replace("\\", "\\"));
        if (path.StartsWith("HKCU:\\", StringComparison.OrdinalIgnoreCase))
            return (Registry.CurrentUser, path.Substring(6).TrimStart('\\').Replace("\\", "\\"));
        return null;
    }

    private static bool RegKeyExists(RegistryKey root, string subKey)
    {
        try
        {
            using var k = root.OpenSubKey(subKey);
            return k != null;
        }
        catch { return false; }
    }

    private static bool ServiceExists(string name)
    {
        try
        {
            using var s = new ServiceController(name);
            return true;
        }
        catch { return false; }
    }

    private static (string path, string name) SplitTaskValue(string v)
    {
        if (string.IsNullOrEmpty(v)) return ("\\", "");
        var idx = v.IndexOf("::", StringComparison.Ordinal);
        if (idx >= 0) return (v.Substring(0, idx).TrimEnd('\\') + "\\", v.Substring(idx + 2));
        var last = v.LastIndexOf('\\');
        if (last >= 0) return (v.Substring(0, last + 1), v.Substring(last + 1));
        return ("\\", v);
    }

    private static bool ScheduledTaskExists(string taskPath, string taskName)
    {
        try
        {
            var tn = (taskPath.TrimEnd('\\') + "\\" + taskName).TrimStart('\\');
            var output = RunProcessOutput("schtasks", $"/query /tn \"{tn}\"");
            return output != null && !output.Contains("ERROR");
        }
        catch { return false; }
    }

    private static bool FirewallRuleExists(string displayName)
    {
        try
        {
            var output = RunProcessOutput("netsh", $"advfirewall firewall show rule name=\"{displayName}\"");
            return !string.IsNullOrEmpty(output) && !output.Contains("No rules match");
        }
        catch { return false; }
    }

    private static string RunProcessOutput(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var outStr = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(15000);
            return outStr;
        }
        catch { return ""; }
    }

    private static void RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            p?.WaitForExit(15000);
        }
        catch { }
    }

    public static Task<ToolResult> RunCleanupAsync(IReadOnlyList<ZeroTraceTarget> targets, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!IsAdmin())
                return new ToolResult(false, "Administrator privileges required. Right-click Sentinel → Run as administrator.");

            var lockedPaths = new List<string>();
            int pathsRemoved = 0, regRemoved = 0, servicesRemoved = 0, tasksRemoved = 0, fwRemoved = 0, errors = 0;

            foreach (var t in targets)
            {
                if (!t.Exists) continue;
                if (ct.IsCancellationRequested) break;

                switch (t.Kind)
                {
                    case ZeroTraceTargetKind.Path:
                        if (t.Blocked) continue;
                        if (TryRemovePath(t.Value)) pathsRemoved++; else lockedPaths.Add(t.Value);
                        break;
                    case ZeroTraceTargetKind.RegistryKey:
                        if (TryRemoveRegistryKey(t.Value)) regRemoved++; else errors++;
                        break;
                    case ZeroTraceTargetKind.Service:
                        if (TryRemoveService(t.Value)) servicesRemoved++; else errors++;
                        break;
                    case ZeroTraceTargetKind.ScheduledTask:
                        if (TryRemoveScheduledTask(t.Value)) tasksRemoved++; else errors++;
                        break;
                    case ZeroTraceTargetKind.FirewallRule:
                        if (TryRemoveFirewallRule(t.Value)) fwRemoved++; else errors++;
                        break;
                }
            }

            if (lockedPaths.Count > 0)
                ScheduleRunOnceDeletes(lockedPaths);

            var msg = $"Paths: {pathsRemoved}, Registry: {regRemoved}, Services: {servicesRemoved}, Tasks: {tasksRemoved}, Firewall: {fwRemoved}.";
            if (lockedPaths.Count > 0) msg += $" {lockedPaths.Count} locked path(s) scheduled for deletion after reboot.";
            if (errors > 0) msg += $" {errors} item(s) could not be removed.";
            return new ToolResult(true, msg);
        }, ct);
    }

    private static bool TryRemovePath(string path)
    {
        if (string.IsNullOrEmpty(path) || (!Directory.Exists(path) && !File.Exists(path))) return true;
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            else
                File.Delete(path);
            return true;
        }
        catch { return false; }
    }

    private static bool TryRemoveRegistryKey(string path)
    {
        var p = RegPathToKey(path);
        if (p == null) return true;
        try
        {
            p.Value.root.DeleteSubKeyTree(p.Value.subKey, throwOnMissingSubKey: false);
            return true;
        }
        catch { return false; }
    }

    private static bool TryRemoveService(string name)
    {
        try
        {
            using var svc = new ServiceController(name);
            if (svc.Status != ServiceControllerStatus.Stopped)
            {
                svc.Stop();
                svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
            RunProcess("sc", $"delete \"{name}\"");
            return true;
        }
        catch { return false; }
    }

    private static bool TryRemoveScheduledTask(string value)
    {
        var (path, name) = SplitTaskValue(value);
        var tn = (path.TrimEnd('\\') + "\\" + name).TrimStart('\\');
        try
        {
            RunProcess("schtasks", $"/delete /tn \"{tn}\" /f");
            return true;
        }
        catch { return false; }
    }

    private static bool TryRemoveFirewallRule(string displayName)
    {
        try
        {
            RunProcess("netsh", $"advfirewall firewall delete rule name=\"{displayName}\"");
            return true;
        }
        catch { return false; }
    }

    private static void ScheduleRunOnceDeletes(List<string> paths)
    {
        try
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), "ZeroTrace_RunOnceDelete_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".ps1");
            var lines = new List<string>
            {
                "$ErrorActionPreference='SilentlyContinue'",
                "function Rm($p){ if(Test-Path -LiteralPath $p){ Remove-Item -LiteralPath $p -Recurse -Force } }"
            };
            foreach (var p in paths.Distinct())
                lines.Add("Rm '" + p.Replace("'", "''") + "'");
            lines.Add("Remove-Item -LiteralPath '" + scriptPath.Replace("'", "''") + "' -Force");
            File.WriteAllLines(scriptPath, lines);
            var cmd = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
            using var runOnce = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", true);
            runOnce?.SetValue("ZeroTraceDelete_" + Guid.NewGuid().ToString("N"), cmd);
        }
        catch { }
    }

    private static bool IsAdmin()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
