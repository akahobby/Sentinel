using Sentinel.Core.Formatting;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Core.Analysis;

public sealed class AnalyzerService : IAnalyzerService
{
    private readonly IProcessCollector _processCollector;
    private readonly IStorageService? _storage;

    public AnalyzerService(IProcessCollector processCollector, IStorageService? storage = null)
    {
        _processCollector = processCollector;
        _storage = storage;
    }

    public async Task<IReadOnlyList<AnalyzerFinding>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<AnalyzerFinding>();
        var processes = new List<ProcessInfo>();
        await foreach (var p in _processCollector.EnumerateAsync(cancellationToken).ConfigureAwait(false))
            processes.Add(p);

        var topCpu = processes.OrderByDescending(x => x.CpuPercent).Take(10).ToList();
        var topMem = processes.OrderByDescending(x => x.MemoryMb).Take(10).ToList();
        var totalCpu = processes.Sum(x => x.CpuPercent);
        var totalMemMb = processes.Sum(x => x.MemoryMb);

        if (totalCpu > 80)
        {
            var topCpuList = string.Join("; ", topCpu.Take(5).Select(x => $"{x.Name} ({UsageFormat.CpuPercent(x.CpuPercent)})"));
            findings.Add(new AnalyzerFinding
            {
                Title = "High CPU usage",
                Category = "Cpu",
                Severity = Severity.Warn,
                Explanation = "Total CPU usage from top processes is high, which can make the system feel slow.",
                Evidence = $"Total: {UsageFormat.CpuPercent(totalCpu)}. Top consumers: {topCpuList}.",
                RecommendedActions = new[] { "Identify and close or limit heavy applications.", "Check for runaway processes in Processes tab." },
                QuickActions = topCpu.Take(2).Select(x => new QuickAction { Label = "Open " + x.Name, ActionType = "OpenProcess", Target = x.Pid.ToString() }).ToList()
            });
        }

        if (totalMemMb > 12000) // heuristic: >12GB total process memory
        {
            var topMemList = string.Join("; ", topMem.Take(5).Select(x => $"{x.Name} ({UsageFormat.MemoryMb(x.MemoryMb)})"));
            findings.Add(new AnalyzerFinding
            {
                Title = "High memory usage",
                Category = "Memory",
                Severity = Severity.Warn,
                Explanation = "Total process memory is very high, which can cause paging and slowdowns.",
                Evidence = $"Total: {UsageFormat.MemoryMb(totalMemMb)}. Top consumers: {topMemList}. Consider closing unused apps.",
                RecommendedActions = new[] { "Close unnecessary applications.", "Check for memory leaks in History." },
                QuickActions = topMem.Take(2).Select(x => new QuickAction { Label = "Open " + x.Name, ActionType = "OpenProcess", Target = x.Pid.ToString() }).ToList()
            });
        }

        if (findings.Count == 0)
            findings.Add(new AnalyzerFinding
            {
                Title = "No major issues detected",
                Category = "System",
                Severity = Severity.Ok,
                Explanation = "CPU and memory usage appear normal. Run History and check for spikes if you experience slowness.",
                Evidence = "",
                RecommendedActions = Array.Empty<string>()
            });

        // Record scan completion so History page has data
        try
        {
            if (_storage != null)
                await _storage.WriteChangeEventAsync(new ChangeEvent
                {
                    DetectedUtc = DateTime.UtcNow,
                    Category = "Scan",
                    ChangeType = "Completed",
                    Name = "Analysis",
                    Details = $"{findings.Count} finding(s). Total memory: {UsageFormat.MemoryMb(totalMemMb)}, total CPU: {UsageFormat.CpuPercent(totalCpu)}.",
                    IsApproved = false,
                    IsIgnored = false
                }, cancellationToken).ConfigureAwait(false);
        }
        catch { /* best effort */ }

        return findings;
    }
}
