namespace Sentinel.Core.Models;

public sealed class ProcessSample
{
    public DateTime TimestampUtc { get; set; }
    public int Pid { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public double DiskKbps { get; set; }
    public double NetworkKbps { get; set; }
    public double? GpuPercent { get; set; }
}
