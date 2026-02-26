using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IServicesCollector
{
    Task<IReadOnlyList<ServiceInfo>> GetAllAsync(CancellationToken cancellationToken = default);
    Task StartAsync(string serviceName, CancellationToken cancellationToken = default);
    Task StopAsync(string serviceName, CancellationToken cancellationToken = default);
    Task SetStartTypeAsync(string serviceName, string startType, CancellationToken cancellationToken = default);
}
