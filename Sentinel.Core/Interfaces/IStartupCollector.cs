using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IStartupCollector
{
    Task<IReadOnlyList<StartupItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task EnableAsync(StartupItem item, CancellationToken cancellationToken = default);
    Task DisableAsync(StartupItem item, CancellationToken cancellationToken = default);
}
