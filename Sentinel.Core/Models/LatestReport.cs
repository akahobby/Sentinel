using System.Text.Json.Serialization;

namespace Sentinel.Core.Models;

public sealed class LatestReport
{
    [JsonPropertyName("generatedUtc")]
    public DateTime GeneratedUtc { get; set; }

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "";

    [JsonPropertyName("systemSnapshot")]
    public SystemSnapshot? SystemSnapshot { get; set; }

    [JsonPropertyName("topOffenders")]
    public TopOffenders? TopOffenders { get; set; }

    [JsonPropertyName("lastScanResults")]
    public object? LastScanResults { get; set; }

    [JsonPropertyName("lastBootMeasurement")]
    public object? LastBootMeasurement { get; set; }

    [JsonPropertyName("recentSpikes")]
    public IReadOnlyList<SpikeEvent> RecentSpikes { get; set; } = Array.Empty<SpikeEvent>();

    [JsonPropertyName("recentChanges")]
    public IReadOnlyList<ChangeEvent> RecentChanges { get; set; } = Array.Empty<ChangeEvent>();
}

public sealed class SystemSnapshot
{
    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = "";

    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = "";

    [JsonPropertyName("totalPhysicalMemoryMb")]
    public double TotalPhysicalMemoryMb { get; set; }

    [JsonPropertyName("availableMemoryMb")]
    public double AvailableMemoryMb { get; set; }

    [JsonPropertyName("processorCount")]
    public int ProcessorCount { get; set; }
}

public sealed class TopOffenders
{
    [JsonPropertyName("cpu")]
    public IReadOnlyList<ProcessInfo> Cpu { get; set; } = Array.Empty<ProcessInfo>();

    [JsonPropertyName("memory")]
    public IReadOnlyList<ProcessInfo> Memory { get; set; } = Array.Empty<ProcessInfo>();

    [JsonPropertyName("disk")]
    public IReadOnlyList<ProcessInfo> Disk { get; set; } = Array.Empty<ProcessInfo>();

    [JsonPropertyName("network")]
    public IReadOnlyList<ProcessInfo> Network { get; set; } = Array.Empty<ProcessInfo>();
}
