using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Platform.Collectors;

public sealed class ProcessCollector : IProcessCollector
{
    private static readonly Dictionary<int, (long Total, long Timestamp)> _prevCpu = new();
    private static readonly int ProcessorCount = Environment.ProcessorCount;

    public async IAsyncEnumerable<ProcessInfo> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // First pass: snapshot CPU times so we can compute % after a short delay
        var processes0 = Process.GetProcesses();
        var nowTicks = DateTime.UtcNow.Ticks;
        try
        {
            foreach (var p in processes0)
            {
                try
                {
                    var total = p.TotalProcessorTime.Ticks;
                    _prevCpu[p.Id] = (total, nowTicks);
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        finally
        {
            foreach (var p in processes0) p.Dispose();
        }

        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        var processes = Process.GetProcesses();
        var gpuByPid = GpuUsageReader.GetGpuPercentByPid();
        var networkKbpsByPid = NetworkUsageReader.GetNetworkKbpsByPid();
        var parentByPid = Native.GetParentPidMap();
        try
        {
            foreach (var p in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Use current time per process so elapsed matches actual wall clock between the two CPU samples.
                // Using a single timestamp from loop start would make late-in-loop processes (e.g. Sentinel) show inflated CPU.
                var sampleTicks = DateTime.UtcNow.Ticks;
                ProcessInfo? info = null;
                try
                {
                    info = await GetProcessInfoAsync(p, sampleTicks, gpuByPid, networkKbpsByPid, parentByPid, includeCommandLine: false, includePath: false).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                finally
                {
                    p.Dispose();
                }
                if (info != null)
                    yield return info;
            }
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    public async Task<ProcessInfo?> GetByPidAsync(int pid, CancellationToken cancellationToken = default)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return await GetProcessInfoAsync(p, DateTime.UtcNow.Ticks, includeCommandLine: true).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    public Task<string?> GetCommandLineAsync(int pid, CancellationToken cancellationToken = default)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return Task.FromResult(GetCommandLineInternal(p.Id));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task<IReadOnlySet<int>> GetProcessIdsWithVisibleWindowsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => (IReadOnlySet<int>)WindowHelper.GetProcessIdsWithVisibleWindows(), cancellationToken);
    }

    private static async Task<ProcessInfo?> GetProcessInfoAsync(Process p, long nowTicks,
        IReadOnlyDictionary<int, double>? gpuByPid = null,
        IReadOnlyDictionary<int, double>? networkKbpsByPid = null,
        IReadOnlyDictionary<int, int>? parentByPid = null,
        bool includeCommandLine = false,
        bool includePath = true)
    {
        string? path = null;
        if (includePath)
        {
            try { path = p.MainModule?.FileName; } catch { }
        }

        long totalTime = 0;
        try { totalTime = p.TotalProcessorTime.Ticks; } catch { }

        double cpu = 0;
        var key = p.Id;
        if (_prevCpu.TryGetValue(key, out var prev))
        {
            var elapsedTicks = nowTicks - prev.Timestamp;
            var elapsedSeconds = elapsedTicks / (double)TimeSpan.TicksPerSecond;
            if (elapsedSeconds > 0.1 && ProcessorCount > 0)
            {
                var cpuDeltaTicks = totalTime - prev.Total;
                if (cpuDeltaTicks >= 0)
                    cpu = Math.Min(100, (cpuDeltaTicks / (double)TimeSpan.TicksPerSecond) / elapsedSeconds * 100.0 / ProcessorCount);
            }
        }
        _prevCpu[key] = (totalTime, nowTicks);

        double memMb = 0;
        try { memMb = p.WorkingSet64 / (1024.0 * 1024.0); } catch { }

        int? parentPid = null;
        string? cmdLine = null;
        if (parentByPid != null && parentByPid.TryGetValue(p.Id, out var pout))
            parentPid = pout;
        else
            parentPid = GetParentPid(p.Id);
        if (includeCommandLine)
            cmdLine = GetCommandLineInternal(p.Id);

        var gpu = 0.0;
        var networkKbps = 0.0;
        if (gpuByPid != null) gpuByPid.TryGetValue(p.Id, out gpu);
        if (networkKbpsByPid != null) networkKbpsByPid.TryGetValue(p.Id, out networkKbps);

        return await Task.FromResult(new ProcessInfo
        {
            Pid = p.Id,
            Name = p.ProcessName,
            Path = path,
            CommandLine = cmdLine,
            ParentPid = parentPid,
            CpuPercent = Math.Round(cpu, 2),
            MemoryMb = Math.Round(memMb, 2),
            GpuPercent = Math.Round(gpu, 2),
            DiskKbps = 0,
            NetworkKbps = Math.Round(networkKbps, 2),
            IsRunning = true
        }).ConfigureAwait(false);
    }

    private static int? GetParentPid(int pid)
    {
        try
        {
            return Native.GetParentProcessId(pid);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCommandLineInternal(int pid)
    {
        try
        {
            return Native.GetCommandLine(pid);
        }
        catch
        {
            return null;
        }
    }

    private static class Native
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        public static int? GetParentProcessId(int pid)
        {
            var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
                return null;
            try
            {
                var pe = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
                if (!Process32First(snapshot, ref pe))
                    return null;
                do
                {
                    if (pe.th32ProcessID == (uint)pid)
                        return (int)pe.th32ParentProcessID;
                } while (Process32Next(snapshot, ref pe));
                return null;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        public static IReadOnlyDictionary<int, int> GetParentPidMap()
        {
            var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
                return new Dictionary<int, int>();
            var dict = new Dictionary<int, int>();
            try
            {
                var pe = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
                if (!Process32First(snapshot, ref pe))
                    return dict;
                do
                {
                    dict[(int)pe.th32ProcessID] = (int)pe.th32ParentProcessID;
                } while (Process32Next(snapshot, ref pe));
            }
            finally
            {
                CloseHandle(snapshot);
            }
            return dict;
        }

        private const uint TH32CS_SNAPPROCESS = 2;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        public static string? GetCommandLine(int pid)
        {
            try
            {
                return GetCommandLineWmi(pid);
            }
            catch
            {
                return null;
            }
        }

        private static string? GetCommandLineWmi(int pid)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var cmd = obj["CommandLine"]?.ToString();
                    if (!string.IsNullOrEmpty(cmd)) return cmd;
                }
            }
            catch { }
            return null;
        }
    }
}
