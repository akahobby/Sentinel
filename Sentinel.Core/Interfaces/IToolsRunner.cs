namespace Sentinel.Core.Interfaces;

/// <summary>Runs the integrated tools (tempdel, USB power-saving disabler, TPM fix, ZeroTrace).</summary>
public interface IToolsRunner
{
    /// <summary>Cleans temporary files from %TEMP%, Windows\Temp, and Prefetch. Admin required for system paths.</summary>
    Task<ToolResult> RunTempCleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>Disables USB power-saving via WMI to reduce sleep/disconnect issues. Admin required.</summary>
    Task<ToolResult> RunUsbPowerDisableAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks TPM attestation and restores provisioning tasks if not ready. Admin required.</summary>
    Task<ToolResult> RunTpmAttestationFixAsync(CancellationToken cancellationToken = default);

    // ZeroTrace (built-in engine)

    /// <summary>Enumerates installed apps from Uninstall registry for ZeroTrace app picker. When skipSizeCalculation is true, returns quickly without computing folder sizes.</summary>
    Task<IReadOnlyList<ZeroTraceApp>> GetInstalledAppsForZeroTraceAsync(CancellationToken cancellationToken = default, bool skipSizeCalculation = false);

    /// <summary>Scans for residual targets (paths, registry, services, tasks, firewall) for the given app.</summary>
    Task<IReadOnlyList<ZeroTraceTarget>> ScanZeroTraceAsync(ZeroTraceApp app, bool fullCleanup, CancellationToken cancellationToken = default);

    /// <summary>Runs the application's uninstaller (QuietUninstallString or UninstallString). Returns when the process exits or times out.</summary>
    Task<ToolResult> RunUninstallAsync(ZeroTraceApp app, CancellationToken cancellationToken = default);

    /// <summary>Removes the selected residual targets. Admin required. Locked paths may be scheduled for RunOnce after reboot.</summary>
    Task<ToolResult> RunZeroTraceCleanupAsync(IReadOnlyList<ZeroTraceTarget> targets, CancellationToken cancellationToken = default);

    /// <summary>Launches Win11Reclaim (external PowerShell toolkit) with elevation using the Get.ps1 bootstrapper.</summary>
    Task<ToolResult> RunWin11ReclaimAsync(CancellationToken cancellationToken = default);
}

public sealed record ToolResult(bool Success, string Message);
