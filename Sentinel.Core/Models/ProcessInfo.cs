namespace Sentinel.Core.Models;

public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string? Path { get; set; }
    public string? CommandLine { get; set; }
    public int? ParentPid { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public double GpuPercent { get; set; }
    public double DiskKbps { get; set; }
    public double NetworkKbps { get; set; }
    public string? Publisher { get; set; }
    public bool IsSigned { get; set; }
    public bool IsSignatureValid { get; set; }
    public string? Sha256 { get; set; }
    public RiskLevel Risk { get; set; }
    public bool IsRunning { get; set; } = true;
    public DateTime? FirstSeenUtc { get; set; }
}

public enum RiskLevel
{
    Unknown,
    Ok,
    Low,
    Medium,
    High,
    Suspicious
}
