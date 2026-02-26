using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

/// <summary>Provides system information for the Overview dashboard.</summary>
public interface ISystemInfoProvider
{
    Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default);
}
