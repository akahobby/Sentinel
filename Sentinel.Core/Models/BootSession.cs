namespace Sentinel.Core.Models;

public sealed class BootSession
{
    public long Id { get; set; }
    public DateTime BootTimeUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public double? ImpactScore { get; set; }
}
