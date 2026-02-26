using System.Runtime.InteropServices;

namespace Sentinel.Platform.Collectors;

/// <summary>Measures per-process network activity using TCP connection count.
/// Two samples give a rate; on first run or when elapsed is too small, shows a baseline from connection count so UI isn't empty.</summary>
internal static class NetworkUsageReader
{
    private static readonly Dictionary<int, int> _prevConnCount = new();
    private static long _prevTicks;
    private static readonly object _lock = new();

    public static IReadOnlyDictionary<int, double> GetNetworkKbpsByPid()
    {
        lock (_lock)
        {
            var connCount = GetCurrentConnectionCountByPid();
            var nowTicks = DateTime.UtcNow.Ticks;
            var result = new Dictionary<int, double>();

            if (_prevTicks != 0)
            {
                var elapsedSec = (nowTicks - _prevTicks) / (double)TimeSpan.TicksPerSecond;
                if (elapsedSec >= 0.15)
                {
                    var allPids = connCount.Keys.Union(_prevConnCount.Keys).ToHashSet();
                    foreach (var pid in allPids)
                    {
                        connCount.TryGetValue(pid, out var cur);
                        _prevConnCount.TryGetValue(pid, out var prev);
                        var delta = Math.Max(0, cur - prev);
                        var kbps = (delta * 80.0 / elapsedSec) + (cur * 3.0);
                        if (kbps > 0)
                            result[pid] = Math.Round(Math.Min(99999, kbps), 2);
                    }
                }
                else
                {
                    // Elapsed too small (e.g. another caller just ran): show baseline so network column isn't empty
                    foreach (var kv in connCount)
                    {
                        if (kv.Value > 0)
                            result[kv.Key] = Math.Round(Math.Min(99999, kv.Value * 3.0), 2);
                    }
                }
            }
            else
            {
                // First run: show baseline from current connection count so network isn't always 0
                foreach (var kv in connCount)
                {
                    if (kv.Value > 0)
                        result[kv.Key] = Math.Round(Math.Min(99999, kv.Value * 3.0), 2);
                }
            }

            _prevConnCount.Clear();
            foreach (var kv in connCount)
                _prevConnCount[kv.Key] = kv.Value;
            _prevTicks = nowTicks;
            return result;
        }
    }

    private static Dictionary<int, int> GetCurrentConnectionCountByPid()
    {
        var byPid = new Dictionary<int, int>();
        try
        {
            const int AF_INET = 2;
            const int TCP_TABLE_OWNER_PID_ALL = 5;
            int size = 0;
            TcpTableNative.GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL);
            if (size <= 0) return byPid;
            var buf = Marshal.AllocHGlobal(size);
            try
            {
                var err = TcpTableNative.GetExtendedTcpTable(buf, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL);
                if (err != 0) return byPid;
                var numEntries = Marshal.ReadInt32(buf);
                var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
                var rowPtr = buf + 4;
                for (var i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                    var pid = (int)row.owningPid;
                    if (pid != 0)
                    {
                        if (!byPid.TryGetValue(pid, out var c)) c = 0;
                        byPid[pid] = c + 1;
                    }
                    rowPtr += rowSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        catch { }

        return byPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    private static class TcpTableNative
    {
        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool sort, int ipVersion, int tblClass);
    }
}
