namespace Sentinel.Core.Models;

public sealed class StartupItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Location { get; set; } = ""; // Run key, Folder, Task
    public bool IsEnabled { get; set; }
    public string? Publisher { get; set; }
    public bool IsSigned { get; set; }
    public bool IsSignatureValid { get; set; }
    public string? Path { get; set; }
    public StartupImpact Impact { get; set; }
    public string? RevertData { get; set; } // Stored for enable/disable revert

    /// <summary>Display label for list UI.</summary>
    public string StatusLabel => IsEnabled ? "Enabled" : "Disabled";
}

public enum StartupImpact
{
    Unknown,
    Low,
    Medium,
    High
}
