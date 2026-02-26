namespace Sentinel.Core.Interfaces;

/// <summary>Provides live system performance metrics and history for Task-Manager-style graphs.</summary>
public interface ISystemPerformanceService
{
    double CpuPercent { get; }
    double MemoryPercent { get; }
    double GpuPercent { get; }
    double NetworkKbps { get; }
    /// <summary>Last N CPU samples (0-100), newest last. Length is at most 60.</summary>
    IReadOnlyList<double> CpuHistory { get; }
    /// <summary>Last N Memory samples (0-100), newest last.</summary>
    IReadOnlyList<double> MemoryHistory { get; }
    /// <summary>Last N GPU samples (0-100), newest last.</summary>
    IReadOnlyList<double> GpuHistory { get; }
    /// <summary>Last N Network samples (Kbps), newest last.</summary>
    IReadOnlyList<double> NetworkHistory { get; }
    /// <summary>Take one sample (call every ~1 second from UI timer).</summary>
    Task SampleAsync(CancellationToken cancellationToken = default);
}
