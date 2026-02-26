using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sentinel.Platform.Collectors;

/// <summary>Reads per-process GPU utilization from Windows "GPU Engine" performance counters (when available).</summary>
internal static class GpuUsageReader
{
    private static readonly Regex PidMatch = new(@"pid_(\d+)_", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static bool _categoryChecked;
    private static bool _categoryExists;
    /// <summary>Cache counters by instance name so NextValue() returns real data after the first sample (first call returns 0).</summary>
    private static readonly Dictionary<string, PerformanceCounter> _counterCache = new();
    private static readonly object _cacheLock = new();

    public static IReadOnlyDictionary<int, double> GetGpuPercentByPid()
    {
        var result = new Dictionary<int, double>();
        try
        {
            if (!_categoryChecked)
            {
                _categoryExists = PerformanceCounterCategory.Exists("GPU Engine");
                _categoryChecked = true;
            }
            if (!_categoryExists)
                return result;

            var cat = new PerformanceCounterCategory("GPU Engine");
            var counterName = "Utilization Percentage";
            var instances = cat.GetInstanceNames();
            foreach (var instance in instances)
            {
                var m = PidMatch.Match(instance);
                if (!m.Success) continue;
                if (!int.TryParse(m.Groups[1].Value, out var pid)) continue;
                try
                {
                    var val = GetCounterValue(instance, counterName);
                    if (val > 0)
                    {
                        if (!result.TryGetValue(pid, out var existing))
                            existing = 0;
                        result[pid] = existing + val;
                    }
                }
                catch
                {
                    RemoveCachedCounter(instance);
                }
            }

            var pids = result.Keys.ToList();
            foreach (var pid in pids)
            {
                if (result[pid] > 100)
                    result[pid] = 100;
            }
        }
        catch
        {
            // GPU counters not available (e.g. no GPU, or driver doesn't expose them)
        }

        return result;
    }

    private static float GetCounterValue(string instanceName, string counterName)
    {
        PerformanceCounter? pc;
        lock (_cacheLock)
        {
            if (!_counterCache.TryGetValue(instanceName, out pc))
            {
                try
                {
                    pc = new PerformanceCounter("GPU Engine", counterName, instanceName, true);
                    _counterCache[instanceName] = pc;
                }
                catch
                {
                    return 0;
                }
            }
        }
        try
        {
            var val = pc.NextValue();
            if (float.IsNaN(val) || val < 0) return 0;
            return val;
        }
        catch
        {
            RemoveCachedCounter(instanceName);
            return 0;
        }
    }

    private static void RemoveCachedCounter(string instanceName)
    {
        lock (_cacheLock)
        {
            if (_counterCache.Remove(instanceName, out var pc))
            {
                try { pc.Dispose(); } catch { }
            }
        }
    }
}
