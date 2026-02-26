namespace Sentinel.Core.Models;

public sealed class ChangeEvent
{
    public long Id { get; set; }
    public DateTime DetectedUtc { get; set; }
    public string Category { get; set; } = ""; // Startup, Service, Task, Process
    public string ChangeType { get; set; } = ""; // Added, Removed, Modified
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Details { get; set; }
    public bool IsApproved { get; set; }
    public bool IsIgnored { get; set; }
}
