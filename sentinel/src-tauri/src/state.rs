use anyhow::{Context, Result};
use chrono::{DateTime, Utc};
use rusqlite::{params, Connection};
use serde::{Deserialize, Serialize};
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone)]
pub struct AppState {
    pub logs_dir: PathBuf,
    pub reports_dir: PathBuf,
    pub exports_dir: PathBuf,
    db_path: PathBuf,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SpikeEvent {
    pub id: Option<i64>,
    pub start_utc: DateTime<Utc>,
    pub end_utc: DateTime<Utc>,
    pub pid: Option<i32>,
    pub process_name: Option<String>,
    pub metric: String,
    pub peak_value: f64,
    pub duration_seconds: f64,
    pub context: Option<String>,
    pub possible_leak: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ChangeEvent {
    pub id: Option<i64>,
    pub detected_utc: DateTime<Utc>,
    pub category: String,
    pub change_type: String,
    pub name: Option<String>,
    pub path: Option<String>,
    pub details: Option<String>,
    pub is_approved: bool,
    pub is_ignored: bool,
}

impl AppState {
    pub fn initialize() -> Result<Self> {
        let base = local_app_data_dir().join("Sentinel");
        let logs = base.join("logs");
        let reports = base.join("reports");
        let exports = base.join("exports");
        let data = base.join("Data");
        let db_path = data.join("sentinel.db");

        ensure_dir(&base)?;
        ensure_dir(&logs)?;
        ensure_dir(&reports)?;
        ensure_dir(&exports)?;
        ensure_dir(&data)?;

        let state = Self {
            logs_dir: logs,
            reports_dir: reports,
            exports_dir: exports,
            db_path,
        };

        state.initialize_db()?;
        state.write_log_line("Sentinel backend initialized")?;
        Ok(state)
    }

    pub fn latest_report_path(&self) -> PathBuf {
        self.reports_dir.join("latest.json")
    }

    pub fn write_log_line(&self, message: &str) -> Result<()> {
        let file_name = format!("{}.log", Utc::now().format("%Y-%m-%d"));
        let log_path = self.logs_dir.join(file_name);
        let mut file = OpenOptions::new()
            .create(true)
            .append(true)
            .open(&log_path)
            .with_context(|| format!("failed to open log file {}", log_path.display()))?;

        writeln!(file, "[{}] {}", Utc::now().to_rfc3339(), message)
            .context("failed to append log line")?;
        Ok(())
    }

    pub fn record_spike_event(&self, event: &SpikeEvent) -> Result<()> {
        let conn = self.open_conn()?;
        conn.execute(
            "INSERT INTO SpikeEvents (StartUtc, EndUtc, Pid, ProcessName, Metric, PeakValue, DurationSeconds, Context, PossibleLeak)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)",
            params![
                event.start_utc.to_rfc3339(),
                event.end_utc.to_rfc3339(),
                event.pid,
                event.process_name,
                event.metric,
                event.peak_value,
                event.duration_seconds,
                event.context,
                if event.possible_leak { 1 } else { 0 }
            ],
        )?;
        Ok(())
    }

    pub fn record_change_event(&self, event: &ChangeEvent) -> Result<()> {
        let conn = self.open_conn()?;
        conn.execute(
            "INSERT INTO ChangeEvents (DetectedUtc, Category, ChangeType, Name, Path, Details, IsApproved, IsIgnored)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![
                event.detected_utc.to_rfc3339(),
                event.category,
                event.change_type,
                event.name,
                event.path,
                event.details,
                if event.is_approved { 1 } else { 0 },
                if event.is_ignored { 1 } else { 0 }
            ],
        )?;
        Ok(())
    }

    pub fn get_spike_events(&self, days_back: i64) -> Result<Vec<SpikeEvent>> {
        let from = Utc::now() - chrono::Duration::days(days_back.max(1));
        let conn = self.open_conn()?;
        let mut stmt = conn.prepare(
            "SELECT Id, StartUtc, EndUtc, Pid, ProcessName, Metric, PeakValue, DurationSeconds, Context, PossibleLeak
             FROM SpikeEvents
             WHERE StartUtc >= ?1
             ORDER BY StartUtc DESC",
        )?;

        let rows = stmt.query_map(params![from.to_rfc3339()], |row| {
            Ok(SpikeEvent {
                id: Some(row.get(0)?),
                start_utc: parse_dt(row.get::<_, String>(1)?).unwrap_or_else(Utc::now),
                end_utc: parse_dt(row.get::<_, String>(2)?).unwrap_or_else(Utc::now),
                pid: row.get(3)?,
                process_name: row.get(4)?,
                metric: row.get(5)?,
                peak_value: row.get(6)?,
                duration_seconds: row.get(7)?,
                context: row.get(8)?,
                possible_leak: row.get::<_, i64>(9)? != 0,
            })
        })?;

        let mut events = Vec::new();
        for item in rows {
            events.push(item?);
        }
        Ok(events)
    }

    pub fn get_change_events(&self, days_back: i64) -> Result<Vec<ChangeEvent>> {
        let from = Utc::now() - chrono::Duration::days(days_back.max(1));
        let conn = self.open_conn()?;
        let mut stmt = conn.prepare(
            "SELECT Id, DetectedUtc, Category, ChangeType, Name, Path, Details, IsApproved, IsIgnored
             FROM ChangeEvents
             WHERE DetectedUtc >= ?1
             ORDER BY DetectedUtc DESC",
        )?;

        let rows = stmt.query_map(params![from.to_rfc3339()], |row| {
            Ok(ChangeEvent {
                id: Some(row.get(0)?),
                detected_utc: parse_dt(row.get::<_, String>(1)?).unwrap_or_else(Utc::now),
                category: row.get(2)?,
                change_type: row.get(3)?,
                name: row.get(4)?,
                path: row.get(5)?,
                details: row.get(6)?,
                is_approved: row.get::<_, i64>(7)? != 0,
                is_ignored: row.get::<_, i64>(8)? != 0,
            })
        })?;

        let mut events = Vec::new();
        for item in rows {
            events.push(item?);
        }
        Ok(events)
    }

    fn open_conn(&self) -> Result<Connection> {
        let conn = Connection::open(&self.db_path)
            .with_context(|| format!("failed to open sqlite db {}", self.db_path.display()))?;
        Ok(conn)
    }

    fn initialize_db(&self) -> Result<()> {
        let conn = self.open_conn()?;
        conn.execute_batch(
            "
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
            ",
        )?;
        Ok(())
    }
}

fn ensure_dir(path: &Path) -> Result<()> {
    fs::create_dir_all(path).with_context(|| format!("failed to create {}", path.display()))?;
    Ok(())
}

fn parse_dt(value: String) -> Option<DateTime<Utc>> {
    DateTime::parse_from_rfc3339(&value)
        .ok()
        .map(|x| x.with_timezone(&Utc))
}

fn local_app_data_dir() -> PathBuf {
    #[cfg(target_os = "windows")]
    {
        if let Ok(path) = std::env::var("LOCALAPPDATA") {
            return PathBuf::from(path);
        }
    }

    dirs::data_local_dir()
        .or_else(dirs::home_dir)
        .unwrap_or_else(|| PathBuf::from("."))
}
