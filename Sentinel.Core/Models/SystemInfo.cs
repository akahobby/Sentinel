namespace Sentinel.Core.Models;

/// <summary>Snapshot of system information for dashboard display (Wintoys-style).</summary>
public record SystemInfo(
    string SystemName,
    string ProcessorName,
    string GraphicsName,
    string MemoryGb,
    string StorageGb,
    string WindowsVersion,
    string UpTimeFormatted
);
