namespace Sentinel.Core.Formatting;

/// <summary>Consistent formatting for usage numbers (e.g. "53.78 MB", "12.34%").</summary>
public static class UsageFormat
{
    public static string MemoryMb(double mb)
    {
        if (mb >= 1024)
            return $"{mb / 1024.0:F2} GB";
        return $"{mb:F2} MB";
    }

    public static string CpuPercent(double pct) => $"{pct:F2}%";
    public static string GpuPercent(double pct) => $"{pct:F2}%";
    public static string NetworkKbps(double kbps) => kbps >= 1024 ? $"{kbps / 1024.0:F2} Mbps" : $"{kbps:F2} Kbps";
}
