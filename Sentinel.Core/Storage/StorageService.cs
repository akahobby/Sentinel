using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Core.Storage;

public sealed class StorageService : IStorageService
{
    private readonly string _dbPath;
    private readonly int _retentionDaysSamples;
    private readonly int _retentionDaysEvents;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public StorageService(int retentionDaysSamples = 7, int retentionDaysEvents = 30)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "Data");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "sentinel.db");
        _retentionDaysSamples = retentionDaysSamples;
        _retentionDaysEvents = retentionDaysEvents;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                _connection = new SqliteConnection($"Data Source={_dbPath}");
                _connection.Open();
                CreateTables();
            }
        }, cancellationToken);
    }

    private void CreateTables()
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ProcessSamples (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampUtc TEXT NOT NULL,
                Pid INTEGER NOT NULL,
                CpuPercent REAL NOT NULL,
                MemoryMb REAL NOT NULL,
                DiskKbps REAL NOT NULL,
                NetworkKbps REAL NOT NULL,
                GpuPercent REAL
            );
            CREATE INDEX IF NOT EXISTS IX_ProcessSamples_Pid_Time ON ProcessSamples(Pid, TimestampUtc);
            CREATE TABLE IF NOT EXISTS SpikeEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartUtc TEXT NOT NULL,
                EndUtc TEXT NOT NULL,
                Pid INTEGER,
                ProcessName TEXT,
                Metric TEXT NOT NULL,
                PeakValue REAL NOT NULL,
                DurationSeconds REAL NOT NULL,
                Context TEXT,
                PossibleLeak INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ChangeEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DetectedUtc TEXT NOT NULL,
                Category TEXT NOT NULL,
                ChangeType TEXT NOT NULL,
                Name TEXT,
                Path TEXT,
                Details TEXT,
                IsApproved INTEGER NOT NULL,
                IsIgnored INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS BootSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BootTimeUtc TEXT NOT NULL,
                LastSeenUtc TEXT,
                ImpactScore REAL
            );
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public Task WriteSamplesAsync(IReadOnlyList<ProcessSample> samples, CancellationToken cancellationToken = default)
    {
        if (samples.Count == 0) return Task.CompletedTask;
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return;
                using var trans = _connection.BeginTransaction();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO ProcessSamples (TimestampUtc, Pid, CpuPercent, MemoryMb, DiskKbps, NetworkKbps, GpuPercent) VALUES (@t, @pid, @cpu, @mem, @disk, @net, @gpu)";
                foreach (var s in samples)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@t", s.TimestampUtc.ToString("O"));
                    cmd.Parameters.AddWithValue("@pid", s.Pid);
                    cmd.Parameters.AddWithValue("@cpu", s.CpuPercent);
                    cmd.Parameters.AddWithValue("@mem", s.MemoryMb);
                    cmd.Parameters.AddWithValue("@disk", s.DiskKbps);
                    cmd.Parameters.AddWithValue("@net", s.NetworkKbps);
                    cmd.Parameters.AddWithValue("@gpu", (object?)s.GpuPercent ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
                trans.Commit();
            }
        }, cancellationToken);
    }

    public Task WriteSpikeEventAsync(SpikeEvent evt, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return;
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO SpikeEvents (StartUtc, EndUtc, Pid, ProcessName, Metric, PeakValue, DurationSeconds, Context, PossibleLeak) VALUES (@s,@e,@p,@pn,@m,@pv,@d,@c,@leak)";
                cmd.Parameters.AddWithValue("@s", evt.StartUtc.ToString("O"));
                cmd.Parameters.AddWithValue("@e", evt.EndUtc.ToString("O"));
                cmd.Parameters.AddWithValue("@p", (object?)evt.Pid ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pn", evt.ProcessName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@m", evt.Metric);
                cmd.Parameters.AddWithValue("@pv", evt.PeakValue);
                cmd.Parameters.AddWithValue("@d", evt.DurationSeconds);
                cmd.Parameters.AddWithValue("@c", evt.Context ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@leak", evt.PossibleLeak ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }, cancellationToken);
    }

    public Task WriteChangeEventAsync(ChangeEvent evt, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return;
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO ChangeEvents (DetectedUtc, Category, ChangeType, Name, Path, Details, IsApproved, IsIgnored) VALUES (@d,@c,@t,@n,@p,@dt,@a,@i)";
                cmd.Parameters.AddWithValue("@d", evt.DetectedUtc.ToString("O"));
                cmd.Parameters.AddWithValue("@c", evt.Category);
                cmd.Parameters.AddWithValue("@t", evt.ChangeType);
                cmd.Parameters.AddWithValue("@n", evt.Name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@p", evt.Path ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@dt", evt.Details ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@a", evt.IsApproved ? 1 : 0);
                cmd.Parameters.AddWithValue("@i", evt.IsIgnored ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ProcessSample>> GetSamplesAsync(int pid, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return (IReadOnlyList<ProcessSample>)Array.Empty<ProcessSample>();
                var list = new List<ProcessSample>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT TimestampUtc, Pid, CpuPercent, MemoryMb, DiskKbps, NetworkKbps, GpuPercent FROM ProcessSamples WHERE Pid = @pid AND TimestampUtc >= @f AND TimestampUtc <= @t ORDER BY TimestampUtc";
                cmd.Parameters.AddWithValue("@pid", pid);
                cmd.Parameters.AddWithValue("@f", fromUtc.ToString("O"));
                cmd.Parameters.AddWithValue("@t", toUtc.ToString("O"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new ProcessSample
                    {
                        TimestampUtc = DateTime.Parse(r.GetString(0)),
                        Pid = r.GetInt32(1),
                        CpuPercent = r.GetDouble(2),
                        MemoryMb = r.GetDouble(3),
                        DiskKbps = r.GetDouble(4),
                        NetworkKbps = r.GetDouble(5),
                        GpuPercent = r.IsDBNull(6) ? null : r.GetDouble(6)
                    });
                }
                return list;
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<SpikeEvent>> GetSpikeEventsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return (IReadOnlyList<SpikeEvent>)Array.Empty<SpikeEvent>();
                var list = new List<SpikeEvent>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Id, StartUtc, EndUtc, Pid, ProcessName, Metric, PeakValue, DurationSeconds, Context, PossibleLeak FROM SpikeEvents WHERE StartUtc >= @f AND StartUtc <= @t ORDER BY StartUtc DESC";
                cmd.Parameters.AddWithValue("@f", fromUtc.ToString("O"));
                cmd.Parameters.AddWithValue("@t", toUtc.ToString("O"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new SpikeEvent
                    {
                        Id = r.GetInt64(0),
                        StartUtc = DateTime.Parse(r.GetString(1)),
                        EndUtc = DateTime.Parse(r.GetString(2)),
                        Pid = r.IsDBNull(3) ? null : r.GetInt32(3),
                        ProcessName = r.IsDBNull(4) ? null : r.GetString(4),
                        Metric = r.GetString(5),
                        PeakValue = r.GetDouble(6),
                        DurationSeconds = r.GetDouble(7),
                        Context = r.IsDBNull(8) ? null : r.GetString(8),
                        PossibleLeak = r.GetInt32(9) != 0
                    });
                }
                return list;
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ChangeEvent>> GetChangeEventsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return (IReadOnlyList<ChangeEvent>)Array.Empty<ChangeEvent>();
                var list = new List<ChangeEvent>();
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT Id, DetectedUtc, Category, ChangeType, Name, Path, Details, IsApproved, IsIgnored FROM ChangeEvents WHERE DetectedUtc >= @f AND DetectedUtc <= @t ORDER BY DetectedUtc DESC";
                cmd.Parameters.AddWithValue("@f", fromUtc.ToString("O"));
                cmd.Parameters.AddWithValue("@t", toUtc.ToString("O"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new ChangeEvent
                    {
                        Id = r.GetInt64(0),
                        DetectedUtc = DateTime.Parse(r.GetString(1)),
                        Category = r.GetString(2),
                        ChangeType = r.GetString(3),
                        Name = r.IsDBNull(4) ? null : r.GetString(4),
                        Path = r.IsDBNull(5) ? null : r.GetString(5),
                        Details = r.IsDBNull(6) ? null : r.GetString(6),
                        IsApproved = r.GetInt32(7) != 0,
                        IsIgnored = r.GetInt32(8) != 0
                    });
                }
                return list;
            }
        }, cancellationToken);
    }

    public Task ApplyRetentionAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_connection == null) return;
                var samplesCutoff = DateTime.UtcNow.AddDays(-_retentionDaysSamples).ToString("O");
                var eventsCutoff = DateTime.UtcNow.AddDays(-_retentionDaysEvents).ToString("O");
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM ProcessSamples WHERE TimestampUtc < @c";
                    cmd.Parameters.AddWithValue("@c", samplesCutoff);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM SpikeEvents WHERE StartUtc < @c";
                    cmd.Parameters.AddWithValue("@c", eventsCutoff);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM ChangeEvents WHERE DetectedUtc < @c";
                    cmd.Parameters.AddWithValue("@c", eventsCutoff);
                    cmd.ExecuteNonQuery();
                }
            }
        }, cancellationToken);
    }
}
