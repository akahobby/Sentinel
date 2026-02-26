use crate::commands::process::{self, ProcessInfo};
use crate::state::{AppState, ChangeEvent, SpikeEvent};
use chrono::Utc;
use serde::Serialize;
use std::fs::File;
use std::io::{Read, Write};
use tauri::State;
use tokio::task;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AnalyzerFinding {
    pub title: String,
    pub category: String,
    pub severity: String,
    pub explanation: String,
    pub evidence: String,
    pub recommended_actions: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemSnapshot {
    pub machine_name: String,
    pub os_version: String,
    pub total_physical_memory_mb: f64,
    pub available_memory_mb: f64,
    pub processor_count: usize,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TopOffenders {
    pub cpu: Vec<ProcessInfo>,
    pub memory: Vec<ProcessInfo>,
    pub disk: Vec<ProcessInfo>,
    pub network: Vec<ProcessInfo>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AnalyzeSystemResponse {
    pub generated_utc: String,
    pub app_version: String,
    pub system_snapshot: SystemSnapshot,
    pub top_offenders: TopOffenders,
    pub findings: Vec<AnalyzerFinding>,
    pub recent_spikes: Vec<SpikeEvent>,
    pub report_path: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SpikeEventsResponse {
    pub spike_events: Vec<SpikeEvent>,
    pub change_events: Vec<ChangeEvent>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ExportReportResponse {
    pub export_path: String,
}

#[tauri::command]
pub async fn analyze_system(state: State<'_, AppState>) -> Result<AnalyzeSystemResponse, String> {
    let processes = process::collect_processes_snapshot().await?;

    let total_cpu = processes.iter().map(|p| p.cpu).sum::<f64>();
    let total_mem = processes.iter().map(|p| p.memory_mb).sum::<f64>();
    let top_cpu = top_by(&processes, |p| p.cpu);
    let top_mem = top_by(&processes, |p| p.memory_mb);
    let top_disk = top_by(&processes, |p| p.disk_kbps);
    let top_network = top_by(&processes, |p| p.network_kbps);

    let snapshot = build_snapshot().await?;
    let findings = build_findings(total_cpu, total_mem, &top_cpu, &top_mem);
    let mut spikes = detect_spikes(&top_cpu);

    if total_cpu > 85.0 {
        spikes.push(SpikeEvent {
            id: None,
            start_utc: Utc::now(),
            end_utc: Utc::now(),
            pid: None,
            process_name: Some("System".to_string()),
            metric: "Cpu".to_string(),
            peak_value: total_cpu,
            duration_seconds: 1.0,
            context: Some("High total CPU usage during analysis".to_string()),
            possible_leak: false,
        });
    }

    let report = AnalyzeSystemResponse {
        generated_utc: Utc::now().to_rfc3339(),
        app_version: "0.1.0".to_string(),
        system_snapshot: snapshot,
        top_offenders: TopOffenders {
            cpu: top_cpu,
            memory: top_mem,
            disk: top_disk,
            network: top_network,
        },
        findings,
        recent_spikes: spikes.clone(),
        report_path: state.latest_report_path().to_string_lossy().to_string(),
    };

    let state_clone = state.inner().clone();
    let report_clone = report.clone();
    let spikes_clone = spikes.clone();
    task::spawn_blocking(move || -> Result<(), String> {
        for spike in &spikes_clone {
            state_clone
                .record_spike_event(spike)
                .map_err(|e| format!("failed to persist spike event: {e}"))?;
        }
        state_clone
            .record_change_event(&ChangeEvent {
                id: None,
                detected_utc: Utc::now(),
                category: "Scan".to_string(),
                change_type: "Completed".to_string(),
                name: Some("Analysis".to_string()),
                path: None,
                details: Some(format!(
                    "{} finding(s), total CPU {:.2}%, total memory {:.2} MB",
                    report_clone.findings.len(),
                    total_cpu,
                    total_mem
                )),
                is_approved: false,
                is_ignored: false,
            })
            .map_err(|e| format!("failed to persist change event: {e}"))?;
        state_clone
            .write_log_line("System analysis completed")
            .map_err(|e| format!("failed to write log line: {e}"))?;
        Ok(())
    })
    .await
    .map_err(|e| format!("analysis write task failed: {e}"))??;

    let report_json =
        serde_json::to_string_pretty(&report).map_err(|e| format!("failed to serialize report: {e}"))?;
    tokio::fs::write(state.latest_report_path(), report_json)
        .await
        .map_err(|e| format!("failed to write latest report: {e}"))?;

    Ok(report)
}

#[tauri::command]
pub async fn get_spike_events(
    state: State<'_, AppState>,
    days_back: Option<i64>,
) -> Result<SpikeEventsResponse, String> {
    let state = state.inner().clone();
    let days = days_back.unwrap_or(7);
    task::spawn_blocking(move || {
        let spikes = state
            .get_spike_events(days)
            .map_err(|e| format!("failed to read spike events: {e}"))?;
        let changes = state
            .get_change_events(days)
            .map_err(|e| format!("failed to read change events: {e}"))?;
        Ok::<SpikeEventsResponse, String>(SpikeEventsResponse {
            spike_events: spikes,
            change_events: changes,
        })
    })
    .await
    .map_err(|e| format!("history read task failed: {e}"))?
}

#[tauri::command]
pub async fn export_report(state: State<'_, AppState>) -> Result<ExportReportResponse, String> {
    let state = state.inner().clone();
    let export_path = task::spawn_blocking(move || {
        let export_name = format!("Sentinel_Report_{}.zip", Utc::now().format("%Y-%m-%d_%H-%M"));
        let export_path = state.exports_dir.join(export_name);

        let file = File::create(&export_path).map_err(|e| format!("failed to create export zip: {e}"))?;
        let mut zip = zip::ZipWriter::new(file);
        let options = zip::write::SimpleFileOptions::default()
            .compression_method(zip::CompressionMethod::Deflated);

        let latest_report = state.latest_report_path();
        if latest_report.exists() {
            let mut content = Vec::new();
            File::open(&latest_report)
                .map_err(|e| format!("failed to open latest report: {e}"))?
                .read_to_end(&mut content)
                .map_err(|e| format!("failed to read latest report: {e}"))?;
            zip.start_file("latest.json", options)
                .map_err(|e| format!("failed to add latest.json to zip: {e}"))?;
            zip.write_all(&content)
                .map_err(|e| format!("failed to write latest.json in zip: {e}"))?;
        }

        if state.logs_dir.exists() {
            for entry in std::fs::read_dir(&state.logs_dir).map_err(|e| format!("failed to read logs dir: {e}"))? {
                let entry = entry.map_err(|e| format!("failed to inspect log file: {e}"))?;
                let path = entry.path();
                if path.extension().is_some_and(|x| x.to_string_lossy().eq_ignore_ascii_case("log")) {
                    let mut content = Vec::new();
                    File::open(&path)
                        .map_err(|e| format!("failed to open log file {}: {e}", path.display()))?
                        .read_to_end(&mut content)
                        .map_err(|e| format!("failed to read log file {}: {e}", path.display()))?;
                    let file_name = path
                        .file_name()
                        .map(|x| x.to_string_lossy().to_string())
                        .unwrap_or_else(|| "sentinel.log".to_string());
                    zip.start_file(format!("logs/{file_name}"), options)
                        .map_err(|e| format!("failed to add log to zip: {e}"))?;
                    zip.write_all(&content)
                        .map_err(|e| format!("failed to write log to zip: {e}"))?;
                }
            }
        }

        zip.finish()
            .map_err(|e| format!("failed to finalize export zip: {e}"))?;
        state
            .write_log_line(&format!("Report exported to {}", export_path.display()))
            .map_err(|e| format!("failed to write export log line: {e}"))?;
        Ok::<String, String>(export_path.to_string_lossy().to_string())
    })
    .await
    .map_err(|e| format!("export task failed: {e}"))??;

    Ok(ExportReportResponse { export_path })
}

fn top_by(processes: &[ProcessInfo], f: impl Fn(&ProcessInfo) -> f64) -> Vec<ProcessInfo> {
    let mut list = processes.to_vec();
    list.sort_by(|a, b| f(b).partial_cmp(&f(a)).unwrap_or(std::cmp::Ordering::Equal));
    list.into_iter().take(10).collect()
}

async fn build_snapshot() -> Result<SystemSnapshot, String> {
    task::spawn_blocking(move || {
        let mut system = sysinfo::System::new_all();
        system.refresh_memory();

        let machine_name = sysinfo::System::host_name().unwrap_or_else(|| "Unknown".to_string());
        let os_name = sysinfo::System::name().unwrap_or_else(|| "Unknown OS".to_string());
        let os_version = sysinfo::System::os_version().unwrap_or_default();
        let pretty_os = format!("{os_name} {os_version}").trim().to_string();

        Ok::<SystemSnapshot, String>(SystemSnapshot {
            machine_name,
            os_version: pretty_os,
            total_physical_memory_mb: system.total_memory() as f64 / (1024.0 * 1024.0),
            available_memory_mb: system.available_memory() as f64 / (1024.0 * 1024.0),
            processor_count: system.cpus().len(),
        })
    })
    .await
    .map_err(|e| format!("snapshot task failed: {e}"))?
}

fn build_findings(
    total_cpu: f64,
    total_mem: f64,
    top_cpu: &[ProcessInfo],
    top_mem: &[ProcessInfo],
) -> Vec<AnalyzerFinding> {
    let mut findings = Vec::new();

    if total_cpu > 80.0 {
        findings.push(AnalyzerFinding {
            title: "High CPU usage".to_string(),
            category: "Cpu".to_string(),
            severity: "warn".to_string(),
            explanation: "Total CPU usage from top processes is high and may degrade responsiveness.".to_string(),
            evidence: format!(
                "Total CPU {:.2}%. Top: {}",
                total_cpu,
                top_cpu
                    .iter()
                    .take(5)
                    .map(|x| format!("{} ({:.2}%)", x.name, x.cpu))
                    .collect::<Vec<_>>()
                    .join(", ")
            ),
            recommended_actions: vec![
                "Close or limit heavy applications.".to_string(),
                "Investigate repeated CPU spikes in History.".to_string(),
            ],
        });
    }

    if total_mem > 12_000.0 {
        findings.push(AnalyzerFinding {
            title: "High memory usage".to_string(),
            category: "Memory".to_string(),
            severity: "warn".to_string(),
            explanation: "Total process memory is high and could trigger paging.".to_string(),
            evidence: format!(
                "Total memory {:.2} MB. Top: {}",
                total_mem,
                top_mem
                    .iter()
                    .take(5)
                    .map(|x| format!("{} ({:.2} MB)", x.name, x.memory_mb))
                    .collect::<Vec<_>>()
                    .join(", ")
            ),
            recommended_actions: vec![
                "Close unused applications.".to_string(),
                "Check for memory leaks in processes with rising usage.".to_string(),
            ],
        });
    }

    if findings.is_empty() {
        findings.push(AnalyzerFinding {
            title: "No major issues detected".to_string(),
            category: "System".to_string(),
            severity: "ok".to_string(),
            explanation: "Current CPU and memory conditions appear healthy.".to_string(),
            evidence: "System usage within expected range.".to_string(),
            recommended_actions: vec!["Continue monitoring with History and Reports.".to_string()],
        });
    }

    findings
}

fn detect_spikes(top_cpu: &[ProcessInfo]) -> Vec<SpikeEvent> {
    let now = Utc::now();
    top_cpu
        .iter()
        .filter(|x| x.cpu >= 80.0)
        .take(3)
        .map(|p| SpikeEvent {
            id: None,
            start_utc: now,
            end_utc: now,
            pid: Some(p.pid),
            process_name: Some(p.name.clone()),
            metric: "Cpu".to_string(),
            peak_value: p.cpu,
            duration_seconds: 1.0,
            context: Some("High process CPU detected during scan".to_string()),
            possible_leak: false,
        })
        .collect()
}
