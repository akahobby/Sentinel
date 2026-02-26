namespace Sentinel.Core.Models;

public sealed class SpikeEvent
{
    public long Id { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int? Pid { get; set; }
    public string? ProcessName { get; set; }
    public string Metric { get; set; } = ""; // Cpu, Memory, Disk, Network
    public double PeakValue { get; set; }
    public double DurationSeconds { get; set; }
    public string? Context { get; set; }
    public bool PossibleLeak { get; set; }
}
