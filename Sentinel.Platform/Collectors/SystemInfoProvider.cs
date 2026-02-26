using System.Management;
using System.Runtime.InteropServices;
using System.Linq;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Platform.Collectors;

public sealed class SystemInfoProvider : ISystemInfoProvider
{
    public Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var systemName = Environment.MachineName;
            var processorName = GetProcessorName();
            var graphicsName = GetGraphicsName();
            var (memoryGb, storageGb) = GetMemoryAndStorage();
            var windowsVersion = GetWindowsVersion();
            var upTime = FormatUptime();

            return new SystemInfo(
                SystemName: systemName,
                ProcessorName: processorName,
                GraphicsName: graphicsName,
                MemoryGb: memoryGb,
                StorageGb: storageGb,
                WindowsVersion: windowsVersion,
                UpTimeFormatted: upTime
            );
        }, cancellationToken);
    }

    private static string GetProcessorName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch { }
        return Environment.ProcessorCount + " core(s)";
    }

    private static string GetGraphicsName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            var names = new List<string>();
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
            if (names.Count > 0) return string.Join(" / ", names.Take(2));
        }
        catch { }
        return "—";
    }

    private static (string memoryGb, string storageGb) GetMemoryAndStorage()
    {
        ulong totalPhys = 0;
        try
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem))
                totalPhys = mem.ullTotalPhys;
        }
        catch { }
        var memoryGb = totalPhys > 0 ? $"{totalPhys / (1024.0 * 1024.0 * 1024.0):F2} GB" : "—";

        double storageGb = 0;
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                storageGb += drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
        }
        catch { }
        var storageStr = storageGb > 0 ? $"{storageGb:F2} TB" : "—";

        return (memoryGb, storageStr);
    }

    private static string GetWindowsVersion()
    {
        try
        {
            var v = Environment.OSVersion.Version;
            if (v.Major >= 10) return "Windows 11";
            if (v.Major == 10) return "Windows 10";
            return "Windows " + v.Major;
        }
        catch { }
        return "—";
    }

    private static string FormatUptime()
    {
        try
        {
            var ms = Math.Max(0, Environment.TickCount64);
            var ts = TimeSpan.FromMilliseconds(ms);
            return $"{(int)ts.TotalDays:D2}:{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        catch { }
        return "—";
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
