namespace Sentinel.Core.Models;

public sealed class ServiceInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "";
    public string StartType { get; set; } = "";
    public string? Description { get; set; }
    public string? BinaryPath { get; set; }
    public string? Publisher { get; set; }
    public bool IsSigned { get; set; }
    public RiskLevel Risk { get; set; }
    public bool RequiresAdmin { get; set; }
}
