namespace Sentinel.Core.Models;

public sealed class AnalyzerFinding
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = ""; // Cpu, Memory, Disk, Network
    public Severity Severity { get; set; }
    public string Explanation { get; set; } = "";
    public string Evidence { get; set; } = "";
    public IReadOnlyList<string> RecommendedActions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<QuickAction> QuickActions { get; set; } = Array.Empty<QuickAction>();
}

public enum Severity
{
    Info,
    Ok,
    Warn,
    Fail
}

public sealed class QuickAction
{
    public string Label { get; set; } = "";
    public string ActionType { get; set; } = ""; // OpenProcess, EndProcess, DisableStartup, OpenFileLocation
    public string? Target { get; set; }
}
