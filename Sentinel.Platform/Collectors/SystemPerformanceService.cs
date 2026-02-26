using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Sentinel.Core.Interfaces;

namespace Sentinel.Platform.Collectors;

public sealed class SystemPerformanceService : ISystemPerformanceService
{
    private const int HistoryLength = 60;
    private readonly List<double> _cpuHistory = new(HistoryLength);
    private readonly List<double> _memoryHistory = new(HistoryLength);
    private readonly List<double> _gpuHistory = new(HistoryLength);
    private readonly List<double> _networkHistory = new(HistoryLength);
    private readonly object _lock = new();
    private static PerformanceCounter? _cpuCounter;
    private static readonly object _cpuCounterLock = new();

    public double CpuPercent { get; private set; }
    public double MemoryPercent { get; private set; }
    public double GpuPercent { get; private set; }
    public double NetworkKbps { get; private set; }
    public IReadOnlyList<double> CpuHistory => _cpuHistory;
    public IReadOnlyList<double> MemoryHistory => _memoryHistory;
    public IReadOnlyList<double> GpuHistory => _gpuHistory;
    public IReadOnlyList<double> NetworkHistory => _networkHistory;

    public Task SampleAsync(CancellationToken cancellationToken = default)
    {
        GetSystemMemory(out var totalPhysBytes, out var availPhysBytes);
        var memoryPercent = totalPhysBytes > 0
            ? Math.Min(100, (1.0 - (availPhysBytes / (double)totalPhysBytes)) * 100.0)
            : 0.0;

        var cpuPercent = GetSystemCpuPercent();

        var gpuByPid = GpuUsageReader.GetGpuPercentByPid();
        var netByPid = NetworkUsageReader.GetNetworkKbpsByPid();
        var gpuPercent = gpuByPid.Values.Sum();
        var networkKbps = netByPid.Values.Sum();

        lock (_lock)
        {
            CpuPercent = cpuPercent;
            MemoryPercent = memoryPercent;
            GpuPercent = gpuPercent;
            NetworkKbps = networkKbps;
            _cpuHistory.Add(cpuPercent);
            if (_cpuHistory.Count > HistoryLength) _cpuHistory.RemoveAt(0);
            _memoryHistory.Add(memoryPercent);
            if (_memoryHistory.Count > HistoryLength) _memoryHistory.RemoveAt(0);
            _gpuHistory.Add(gpuPercent);
            if (_gpuHistory.Count > HistoryLength) _gpuHistory.RemoveAt(0);
            _networkHistory.Add(networkKbps);
            if (_networkHistory.Count > HistoryLength) _networkHistory.RemoveAt(0);
        }

        return Task.CompletedTask;
    }

    private static float GetSystemCpuPercent()
    {
        try
        {
            lock (_cpuCounterLock)
            {
                if (_cpuCounter == null)
                {
                    try
                    {
                        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                    }
                    catch
                    {
                        return 0;
                    }
                }
                var val = _cpuCounter.NextValue();
                if (float.IsNaN(val) || val < 0) return 0;
                return Math.Min(100, val);
            }
        }
        catch
        {
            return 0;
        }
    }

    private static void GetSystemMemory(out ulong totalPhysBytes, out ulong availPhysBytes)
    {
        totalPhysBytes = 0;
        availPhysBytes = 0;
        try
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem))
            {
                totalPhysBytes = mem.ullTotalPhys;
                availPhysBytes = mem.ullAvailPhys;
            }
        }
        catch { }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
