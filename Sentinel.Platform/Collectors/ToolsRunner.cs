using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using Sentinel.Core.Interfaces;

namespace Sentinel.Platform.Collectors;

public sealed class ToolsRunner : IToolsRunner
{
    public async Task<ToolResult> RunTempCleanupAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            var prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

            int totalFiles = 0, totalDirs = 0, errors = 0, skipped = 0;

            CleanDirectoryContents(temp, ref totalFiles, ref totalDirs, ref errors);
            if (IsAdmin())
            {
                CleanDirectoryContents(winTemp, ref totalFiles, ref totalDirs, ref errors);
                CleanDirectoryContents(prefetch, ref totalFiles, ref totalDirs, ref errors);
            }
            else
            {
                skipped = 2;
            }

            var msg = $"Files deleted: {totalFiles}, Folders removed: {totalDirs}.";
            if (skipped > 0) msg += " Run as Administrator to clean Windows\\Temp and Prefetch.";
            if (errors > 0) msg += $" Access issues: {errors}.";
            return new ToolResult(errors == 0 || totalFiles + totalDirs > 0, msg);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void CleanDirectoryContents(string dir, ref int files, ref int dirs, ref int errors)
    {
        if (!Directory.Exists(dir)) return;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(f);
                    files++;
                }
                catch { errors++; }
            }
            var subdirs = Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToList();
            foreach (var d in subdirs)
            {
                try
                {
                    Directory.Delete(d, false);
                    dirs++;
                }
                catch { }
            }
        }
        catch { errors++; }
    }

    public async Task<ToolResult> RunUsbPowerDisableAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!IsAdmin())
                return new ToolResult(false, "Administrator privileges required. Right-click Sentinel → Run as administrator.");

            try
            {
                using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM MSPower_DeviceEnable WHERE InstanceName LIKE '%USB%'");
                int updated = 0, unchanged = 0;
                foreach (ManagementObject mo in searcher.Get())
                {
                    try
                    {
                        var enable = mo["Enable"];
                        if (enable is bool b && b)
                        {
                            mo["Enable"] = false;
                            mo.Put();
                            updated++;
                        }
                        else
                            unchanged++;
                    }
                    catch { }
                }
                return new ToolResult(true, $"Updated: {updated}, Already disabled: {unchanged}, Total USB entries: {updated + unchanged}.");
            }
            catch (Exception ex)
            {
                return new ToolResult(false, "WMI failed: " + ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ToolResult> RunTpmAttestationFixAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!IsAdmin())
                return new ToolResult(false, "Administrator privileges required. Right-click Sentinel → Run as administrator.");

            try
            {
                var (ready, capable) = GetTpmAttestationStatus();
                if (ready && capable)
                    return new ToolResult(true, "TPM attestation is already healthy. No repair needed.");

                var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var taskRoot = Path.Combine(windir, "System32", "Tasks", "Microsoft", "Windows", "TPM");
                var tmpDir = Path.Combine(Path.GetTempPath(), "SentinelTpm_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(tmpDir);

                try
                {
                    foreach (var name in new[] { "Tpm-Maintenance", "Tpm-HASCertRetr", "Tpm-PreAttestationHealthCheck" })
                    {
                        var src = Path.Combine(taskRoot, name);
                        if (!File.Exists(src)) continue;
                        var xml = Path.Combine(tmpDir, name + ".xml");
                        File.Copy(src, xml);
                        RunProcess("schtasks", $"/create /tn \"\\Microsoft\\Windows\\TPM\\{name}\" /xml \"{xml}\" /f");
                    }
                    RunProcess("schtasks", "/run /tn \"\\Microsoft\\Windows\\TPM\\Tpm-Maintenance\"");
                    RunProcess("schtasks", "/run /tn \"\\Microsoft\\Windows\\TPM\\Tpm-HASCertRetr\"");
                    return new ToolResult(true, "Repair completed. Reboot your PC, then run: tpmtool getdeviceinformation");
                }
                finally
                {
                    try { Directory.Delete(tmpDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                return new ToolResult(false, "TPM fix failed: " + ex.Message);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static (bool ready, bool capable) GetTpmAttestationStatus()
    {
        try
        {
            var output = RunProcessOutput("tpmtool", "getdeviceinformation");
            var ready = output.Contains("Ready For Attestation: True", StringComparison.OrdinalIgnoreCase);
            var capable = output.Contains("Is Capable For Attestation: True", StringComparison.OrdinalIgnoreCase);
            return (ready, capable);
        }
        catch
        {
            return (false, false);
        }
    }

    public Task<IReadOnlyList<ZeroTraceApp>> GetInstalledAppsForZeroTraceAsync(CancellationToken cancellationToken = default, bool skipSizeCalculation = false)
        => ZeroTraceEngine.GetInstalledAppsAsync(cancellationToken, skipSizeCalculation);

    public Task<IReadOnlyList<ZeroTraceTarget>> ScanZeroTraceAsync(ZeroTraceApp app, bool fullCleanup, CancellationToken cancellationToken = default)
        => ZeroTraceEngine.ScanAsync(app, fullCleanup, cancellationToken);

    public Task<ToolResult> RunUninstallAsync(ZeroTraceApp app, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var cmd = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
                ? app.QuietUninstallString!.Trim()
                : !string.IsNullOrWhiteSpace(app.UninstallString)
                    ? app.UninstallString!.Trim()
                    : null;
            if (string.IsNullOrEmpty(cmd))
                return new ToolResult(false, "No uninstall command found for this application.");
            try
            {
                string fileName;
                string arguments;
                if (cmd.StartsWith("\"", StringComparison.Ordinal))
                {
                    var endQuote = cmd.IndexOf('"', 1);
                    if (endQuote < 0) { fileName = cmd.Trim('"'); arguments = ""; }
                    else { fileName = cmd[1..endQuote]; arguments = cmd[(endQuote + 1)..].Trim(); }
                }
                else
                {
                    var firstSpace = cmd.IndexOf(' ');
                    if (firstSpace < 0) { fileName = cmd; arguments = ""; }
                    else { fileName = cmd[..firstSpace]; arguments = cmd[(firstSpace + 1)..].Trim(); }
                }
                if (string.IsNullOrWhiteSpace(fileName))
                    return new ToolResult(false, "Invalid uninstall command.");
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                using var p = Process.Start(psi);
                if (p == null)
                    return new ToolResult(false, "Failed to start uninstaller.");
                var exited = p.WaitForExit(300000);
                if (!exited)
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    return new ToolResult(false, "Uninstaller did not exit within 5 minutes. It may still be running.");
                }
                return new ToolResult(true, "Uninstaller finished. Click Find residuals to scan for leftover files and registry entries.");
            }
            catch (Exception ex)
            {
                return new ToolResult(false, "Uninstall failed: " + ex.Message);
            }
        }, cancellationToken);
    }

    public Task<ToolResult> RunZeroTraceCleanupAsync(IReadOnlyList<ZeroTraceTarget> targets, CancellationToken cancellationToken = default)
        => ZeroTraceEngine.RunCleanupAsync(targets, cancellationToken);

    public Task<ToolResult> RunWin11ReclaimAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"iwr 'https://raw.githubusercontent.com/akahobby/Win11Reclaim/main/Get.ps1' -UseBasicParsing | iex\""
                };
                Process.Start(psi);
                return new ToolResult(true, "Win11Reclaim launched in an elevated PowerShell window. Follow its UI to apply tweaks.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new ToolResult(false, "Win11Reclaim launch cancelled (UAC prompt declined).");
            }
            catch (Exception ex)
            {
                return new ToolResult(false, "Failed to launch Win11Reclaim: " + ex.Message);
            }
        }, cancellationToken);
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

    private static void RunProcess(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true };
        using var p = Process.Start(psi);
        p?.WaitForExit(10000);
    }

    private static string RunProcessOutput(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        return p?.StandardOutput.ReadToEnd() ?? "";
    }
}
