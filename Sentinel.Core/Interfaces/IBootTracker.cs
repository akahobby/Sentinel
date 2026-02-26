using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IBootTracker
{
    Task<DateTime?> GetLastBootTimeUtcAsync(CancellationToken cancellationToken = default);
    Task RecordBootSessionAsync(DateTime bootTimeUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BootSession>> GetRecentBootsAsync(int count, CancellationToken cancellationToken = default);
}
