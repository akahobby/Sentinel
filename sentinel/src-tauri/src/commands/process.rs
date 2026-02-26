use crate::commands::trust;
use crate::state::{AppState, ChangeEvent};
use chrono::Utc;
use serde::Serialize;
use std::time::Duration;
use sysinfo::{Pid, ProcessesToUpdate, Signal, System};
use tauri::State;
use tokio::task;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProcessInfo {
    pub pid: i32,
    pub name: String,
    pub cpu: f64,
    #[serde(rename = "memoryMB")]
    pub memory_mb: f64,
    pub path: Option<String>,
    pub signed: bool,
    pub publisher: Option<String>,
    pub risk: String,
    pub command_line: Option<String>,
    pub parent_pid: Option<i32>,
    pub network_kbps: f64,
    pub gpu_percent: f64,
    pub disk_kbps: f64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ListProcessesResponse {
    pub processes: Vec<ProcessInfo>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProcessDetailsResponse {
    pub process: Option<ProcessInfo>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct KillProcessResponse {
    pub success: bool,
    pub pid: i32,
    pub message: String,
}

#[tauri::command]
pub async fn list_processes() -> Result<ListProcessesResponse, String> {
    let processes = collect_processes_snapshot().await?;
    Ok(ListProcessesResponse { processes })
}

#[tauri::command]
pub async fn get_process_details(pid: i32) -> Result<ProcessDetailsResponse, String> {
    let mut target = collect_processes_snapshot()
        .await?
        .into_iter()
        .find(|p| p.pid == pid);

    if let Some(proc_ref) = target.as_mut() {
        if let Some(path) = proc_ref.path.clone() {
            let trust = trust::get_signature_details(&path).await;
            proc_ref.signed = trust.signed;
            proc_ref.publisher = trust.publisher;
            proc_ref.risk =
                trust::assess_risk(Some(&path), proc_ref.publisher.as_deref(), proc_ref.signed, Some(&proc_ref.name));
        }
    }

    Ok(ProcessDetailsResponse { process: target })
}

#[tauri::command]
pub async fn kill_process(pid: i32, state: State<'_, AppState>) -> Result<KillProcessResponse, String> {
    let killed = task::spawn_blocking(move || {
        let mut sys = System::new_all();
        sys.refresh_processes(ProcessesToUpdate::All, true);
        let target_pid = Pid::from_u32(pid.max(0) as u32);
        let Some(process) = sys.process(target_pid) else {
            return Ok::<bool, String>(false);
        };

        let graceful = process.kill_with(Signal::Term).unwrap_or(false);
        let forced = process.kill();
        Ok::<bool, String>(graceful || forced)
    })
    .await
    .map_err(|e| format!("failed to join kill task: {e}"))??;

    let state = state.inner().clone();
    let message = if killed {
        let log_msg = format!("Killed process pid={pid}");
        let _ = task::spawn_blocking(move || {
            let _ = state.write_log_line(&log_msg);
            let _ = state.record_change_event(&ChangeEvent {
                id: None,
                detected_utc: Utc::now(),
                category: "Process".to_string(),
                change_type: "Removed".to_string(),
                name: Some(format!("PID {pid}")),
                path: None,
                details: Some("Process terminated by user action".to_string()),
                is_approved: false,
                is_ignored: false,
            });
        })
        .await;
        "Process terminated.".to_string()
    } else {
        "Process not found or could not be terminated.".to_string()
    };

    Ok(KillProcessResponse {
        success: killed,
        pid,
        message,
    })
}

pub(crate) async fn collect_processes_snapshot() -> Result<Vec<ProcessInfo>, String> {
    task::spawn_blocking(move || {
        let mut system = System::new_all();
        system.refresh_all();
        std::thread::sleep(Duration::from_millis(150));
        system.refresh_processes(ProcessesToUpdate::All, true);

        let mut processes = Vec::new();
        for (pid, p) in system.processes() {
            let pid_i32 = pid.as_u32() as i32;
            let name = p.name().to_string_lossy().to_string();
            let path = p.exe().map(|x| x.to_string_lossy().to_string());
            let command_line = if p.cmd().is_empty() {
                None
            } else {
                Some(
                    p.cmd()
                        .iter()
                        .map(|x| x.to_string_lossy().to_string())
                        .collect::<Vec<_>>()
                        .join(" "),
                )
            };

            let trust_meta = trust::quick_trust_from_path(path.as_deref());
            let risk = trust::assess_risk(
                path.as_deref(),
                trust_meta.publisher.as_deref(),
                trust_meta.signed,
                Some(&name),
            );

            let item = ProcessInfo {
                pid: pid_i32,
                name,
                cpu: (p.cpu_usage() as f64).clamp(0.0, 100.0),
                memory_mb: p.memory() as f64 / (1024.0 * 1024.0),
                path,
                signed: trust_meta.signed,
                publisher: trust_meta.publisher,
                risk,
                command_line,
                parent_pid: p.parent().map(|x| x.as_u32() as i32),
                network_kbps: 0.0,
                gpu_percent: 0.0,
                disk_kbps: 0.0,
            };
            processes.push(item);
        }

        processes.sort_by(|a, b| b.cpu.partial_cmp(&a.cpu).unwrap_or(std::cmp::Ordering::Equal));
        Ok::<Vec<ProcessInfo>, String>(processes)
    })
    .await
    .map_err(|e| format!("failed to collect processes: {e}"))?
}
