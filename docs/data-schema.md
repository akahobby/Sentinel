# Sentinel Data Schema

## SQLite (sentinel.db)

Location: `%LOCALAPPDATA%\Sentinel\Data\sentinel.db`

### ProcessSamples
| Column       | Type    | Description |
|-------------|---------|-------------|
| Id          | INTEGER | PK autoincrement |
| TimestampUtc| TEXT    | ISO 8601 |
| Pid         | INTEGER | Process ID |
| CpuPercent  | REAL    | |
| MemoryMb    | REAL    | |
| DiskKbps    | REAL    | |
| NetworkKbps | REAL    | |
| GpuPercent  | REAL    | nullable |

**Retention**: Configurable (default 7 days). Deleted by `ApplyRetentionAsync`.

### SpikeEvents
| Column         | Type    | Description |
|----------------|---------|-------------|
| Id             | INTEGER | PK |
| StartUtc, EndUtc | TEXT  | ISO 8601 |
| Pid            | INTEGER | nullable |
| ProcessName    | TEXT    | nullable |
| Metric         | TEXT    | Cpu, Memory, Disk, Network |
| PeakValue      | REAL    | |
| DurationSeconds| REAL    | |
| Context        | TEXT    | nullable |
| PossibleLeak   | INTEGER | 0/1 |

**Retention**: Configurable (default 30 days).

### ChangeEvents
| Column      | Type    | Description |
|-------------|---------|-------------|
| Id          | INTEGER | PK |
| DetectedUtc | TEXT   | ISO 8601 |
| Category    | TEXT    | Startup, Service, Task, Process |
| ChangeType  | TEXT    | Added, Removed, Modified |
| Name, Path, Details | TEXT | nullable |
| IsApproved  | INTEGER | 0/1 |
| IsIgnored   | INTEGER | 0/1 |

**Retention**: Same as SpikeEvents (default 30 days).

### BootSessions
| Column       | Type    |
|-------------|---------|
| Id          | INTEGER PK |
| BootTimeUtc | TEXT    |
| LastSeenUtc | TEXT nullable |
| ImpactScore | REAL nullable |

### Settings
| Key   | Value (TEXT) |
|-------|--------------|
| App   | JSON or key-value settings |

---

## latest.json

Location: `%LOCALAPPDATA%\Sentinel\reports\latest.json`

- **generatedUtc**: ISO 8601
- **appVersion**: string
- **systemSnapshot**: machineName, osVersion, totalPhysicalMemoryMb, availableMemoryMb, processorCount
- **topOffenders**: cpu, memory, disk, network (arrays of process summary)
- **lastScanResults**: analyzer findings summary
- **lastBootMeasurement**: optional
- **recentSpikes**: array of spike events
- **recentChanges**: array of change events

Serialized with `System.Text.Json`; written by `IReportExporter.WriteLatestJsonAsync`.

---

## Logs

- **Daily log**: `%LOCALAPPDATA%\Sentinel\logs\YYYY-MM-DD.log` (Serilog file sink).
- **Export ZIP**: `%LOCALAPPDATA%\Sentinel\exports\Sentinel_Report_YYYY-MM-DD_HH-mm.zip` (latest.json + logs).
