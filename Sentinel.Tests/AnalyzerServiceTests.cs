using System.Runtime.CompilerServices;
using Sentinel.Core.Analysis;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Xunit;

namespace Sentinel.Tests;

public class AnalyzerServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_WhenNoProcesses_ReturnsNoMajorIssues()
    {
        var collector = new MockProcessCollector(Array.Empty<ProcessInfo>());
        var analyzer = new AnalyzerService(collector);
        var findings = await analyzer.AnalyzeAsync();
        Assert.NotEmpty(findings);
        var ok = findings.FirstOrDefault(f => f.Severity == Severity.Ok);
        Assert.NotNull(ok);
        Assert.Contains("No major issues", ok.Title);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenHighCpu_ReturnsCpuFinding()
    {
        var processes = new[]
        {
            new ProcessInfo { Pid = 1, Name = "HighCpu", CpuPercent = 50 },
            new ProcessInfo { Pid = 2, Name = "Other", CpuPercent = 45 }
        };
        var collector = new MockProcessCollector(processes);
        var analyzer = new AnalyzerService(collector);
        var findings = await analyzer.AnalyzeAsync();
        var cpuFinding = findings.FirstOrDefault(f => f.Category == "Cpu");
        Assert.NotNull(cpuFinding);
        Assert.Equal(Severity.Warn, cpuFinding.Severity);
    }

    private sealed class MockProcessCollector : IProcessCollector
    {
        private readonly IReadOnlyList<ProcessInfo> _list;

        public MockProcessCollector(IReadOnlyList<ProcessInfo> list) => _list = list;

        public async IAsyncEnumerable<ProcessInfo> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var p in _list)
            {
                await Task.Yield();
                yield return p;
            }
        }

        public Task<ProcessInfo?> GetByPidAsync(int pid, CancellationToken cancellationToken = default) =>
            Task.FromResult(_list.FirstOrDefault(p => p.Pid == pid));

        public Task<string?> GetCommandLineAsync(int pid, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlySet<int>> GetProcessIdsWithVisibleWindowsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }
}
