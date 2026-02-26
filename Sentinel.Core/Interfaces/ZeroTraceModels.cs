namespace Sentinel.Core.Interfaces;

/// <summary>App entry from Windows Uninstall registry or a launcher (e.g. Steam) used by ZeroTrace.</summary>
public sealed record ZeroTraceApp
{
    public string DisplayName { get; }
    public string? Publisher { get; }
    public string? DisplayVersion { get; }
    public string? InstallLocation { get; }
    public string? UninstallString { get; }
    public string? QuietUninstallString { get; }
    public long? SizeBytes { get; }
    public string? Source { get; }

    public ZeroTraceApp(
        string displayName,
        string? publisher,
        string? displayVersion,
        string? installLocation,
        string? uninstallString,
        string? quietUninstallString,
        long? sizeBytes = null,
        string? source = null)
    {
        DisplayName = displayName;
        Publisher = publisher;
        DisplayVersion = displayVersion;
        InstallLocation = installLocation;
        UninstallString = uninstallString;
        QuietUninstallString = quietUninstallString;
        SizeBytes = sizeBytes;
        Source = source;
    }
}

/// <summary>Confidence level for a residual target.</summary>
public enum ZeroTraceConfidence
{
    Low,
    Medium,
    High
}

/// <summary>Kind of residual target.</summary>
public enum ZeroTraceTargetKind
{
    Path,
    RegistryKey,
    Service,
    ScheduledTask,
    FirewallRule
}

/// <summary>Single residual target (path, registry key, service, task, or firewall rule).</summary>
public sealed record ZeroTraceTarget(
    ZeroTraceTargetKind Kind,
    string Value,
    string Source,
    ZeroTraceConfidence Confidence,
    bool Exists,
    bool Blocked,
    string? Meta
);
