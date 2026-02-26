using System.Management;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Platform.Collectors;

public sealed class BootTracker : IBootTracker
{
    public Task<DateTime?> GetLastBootTimeUtcAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var val = obj["LastBootUpTime"]?.ToString();
                if (val != null && ManagementDateTimeConverter.ToDateTime(val) is DateTime dt)
                    return Task.FromResult<DateTime?>(dt.ToUniversalTime());
            }
        }
        catch { }
        return Task.FromResult<DateTime?>(null);
    }

    public Task RecordBootSessionAsync(DateTime bootTimeUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<BootSession>> GetRecentBootsAsync(int count, CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<BootSession>)Array.Empty<BootSession>());
}
