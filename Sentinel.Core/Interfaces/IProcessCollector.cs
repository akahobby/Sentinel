using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IProcessCollector
{
    IAsyncEnumerable<ProcessInfo> EnumerateAsync(CancellationToken cancellationToken = default);
    Task<ProcessInfo?> GetByPidAsync(int pid, CancellationToken cancellationToken = default);
    Task<string?> GetCommandLineAsync(int pid, CancellationToken cancellationToken = default);
    /// <summary>Process IDs that have a window on the taskbar (apps currently in use, like Task Manager's Apps list).</summary>
    Task<IReadOnlySet<int>> GetProcessIdsWithVisibleWindowsAsync(CancellationToken cancellationToken = default);
}
