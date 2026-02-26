using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IStorageService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task WriteSamplesAsync(IReadOnlyList<ProcessSample> samples, CancellationToken cancellationToken = default);
    Task WriteSpikeEventAsync(SpikeEvent evt, CancellationToken cancellationToken = default);
    Task WriteChangeEventAsync(ChangeEvent evt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessSample>> GetSamplesAsync(int pid, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpikeEvent>> GetSpikeEventsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChangeEvent>> GetChangeEventsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task ApplyRetentionAsync(CancellationToken cancellationToken = default);
}
