use crate::state::{AppState, ChangeEvent};
use chrono::Utc;
use serde::Serialize;
use tauri::State;
use tokio::task;

#[cfg(target_os = "windows")]
use crate::commands::trust;
#[cfg(target_os = "windows")]
use tokio::process::Command;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServiceInfo {
    pub name: String,
    pub display_name: String,
    pub status: String,
    pub start_type: String,
    pub description: Option<String>,
    pub binary_path: Option<String>,
    pub signed: bool,
    pub publisher: Option<String>,
    pub risk: String,
    pub requires_admin: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServicesResponse {
    pub services: Vec<ServiceInfo>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServiceActionResponse {
    pub success: bool,
    pub name: String,
    pub action: String,
    pub message: String,
}

#[tauri::command]
pub async fn list_services() -> Result<ServicesResponse, String> {
    let services = list_services_impl().await?;
    Ok(ServicesResponse { services })
}

#[tauri::command]
pub async fn service_action(
    name: String,
    action: String,
    state: State<'_, AppState>,
) -> Result<ServiceActionResponse, String> {
    let result = run_service_action(&name, &action).await;
    match result {
        Ok(message) => {
            let app_state = state.inner().clone();
            let name_clone = name.clone();
            let action_clone = action.clone();
            let msg_clone = message.clone();
            let _ = task::spawn_blocking(move || {
                let _ = app_state.write_log_line(&format!(
                    "Service action name={} action={} result={}",
                    name_clone, action_clone, msg_clone
                ));
                let _ = app_state.record_change_event(&ChangeEvent {
                    id: None,
                    detected_utc: Utc::now(),
                    category: "Service".to_string(),
                    change_type: "Modified".to_string(),
                    name: Some(name_clone),
                    path: None,
                    details: Some(format!("Action '{action_clone}' completed: {msg_clone}")),
                    is_approved: false,
                    is_ignored: false,
                });
            })
            .await;

            Ok(ServiceActionResponse {
                success: true,
                name,
                action,
                message,
            })
        }
        Err(err) => Ok(ServiceActionResponse {
            success: false,
            name,
            action,
            message: err,
        }),
    }
}

#[cfg(target_os = "windows")]
fn parse_binary_path(path: &str) -> Option<String> {
    let trimmed = path.trim();
    if trimmed.is_empty() {
        return None;
    }
    if let Some(stripped) = trimmed.strip_prefix('"') {
        let end = stripped.find('"')?;
        return Some(stripped[..end].to_string());
    }
    Some(trimmed.split_whitespace().next()?.to_string())
}

#[cfg(target_os = "windows")]
async fn list_services_impl() -> Result<Vec<ServiceInfo>, String> {
    let script = "Get-CimInstance Win32_Service | Select-Object Name,DisplayName,State,StartMode,PathName | ConvertTo-Json -Depth 4";
    let output = Command::new("powershell")
        .arg("-NoProfile")
        .arg("-Command")
        .arg(script)
        .output()
        .await
        .map_err(|e| format!("failed to query services: {e}"))?;

    if !output.status.success() {
        return Err(String::from_utf8_lossy(&output.stderr).trim().to_string());
    }

    let value: serde_json::Value =
        serde_json::from_slice(&output.stdout).map_err(|e| format!("invalid services json: {e}"))?;

    let arr = match value {
        serde_json::Value::Array(a) => a,
        serde_json::Value::Object(_) => vec![value],
        _ => Vec::new(),
    };

    let mut services = Vec::new();
    for item in arr {
        let name = item
            .get("Name")
            .and_then(serde_json::Value::as_str)
            .unwrap_or_default()
            .to_string();
        if name.is_empty() {
            continue;
        }
        let display_name = item
            .get("DisplayName")
            .and_then(serde_json::Value::as_str)
            .unwrap_or(&name)
            .to_string();
        let status = item
            .get("State")
            .and_then(serde_json::Value::as_str)
            .unwrap_or("Unknown")
            .to_string();
        let start_type = item
            .get("StartMode")
            .and_then(serde_json::Value::as_str)
            .unwrap_or("Unknown")
            .to_string();
        let raw_path = item
            .get("PathName")
            .and_then(serde_json::Value::as_str)
            .map(|x| x.to_string());
        let binary_path = raw_path.as_deref().and_then(parse_binary_path);
        let trust_meta = trust::quick_trust_from_path(binary_path.as_deref());
        let risk = trust::assess_risk(
            binary_path.as_deref(),
            trust_meta.publisher.as_deref(),
            trust_meta.signed,
            Some(&display_name),
        );

        services.push(ServiceInfo {
            name,
            display_name,
            status,
            start_type,
            description: None,
            binary_path,
            signed: trust_meta.signed,
            publisher: trust_meta.publisher,
            risk,
            requires_admin: false,
        });
    }

    services.sort_by(|a, b| a.display_name.cmp(&b.display_name));
    Ok(services)
}

#[cfg(not(target_os = "windows"))]
async fn list_services_impl() -> Result<Vec<ServiceInfo>, String> {
    Ok(Vec::new())
}

#[cfg(target_os = "windows")]
async fn run_service_action(name: &str, action: &str) -> Result<String, String> {
    let escaped = name.replace('\'', "''");
    let script = match action.to_ascii_lowercase().as_str() {
        "start" => format!("Start-Service -Name '{escaped}'"),
        "stop" => format!("Stop-Service -Name '{escaped}' -Force"),
        "restart" => format!("Restart-Service -Name '{escaped}' -Force"),
        "automatic" => format!("Set-Service -Name '{escaped}' -StartupType Automatic"),
        "manual" => format!("Set-Service -Name '{escaped}' -StartupType Manual"),
        "disabled" => format!("Set-Service -Name '{escaped}' -StartupType Disabled"),
        other => return Err(format!("unsupported service action: {other}")),
    };

    let output = Command::new("powershell")
        .arg("-NoProfile")
        .arg("-Command")
        .arg(&script)
        .output()
        .await
        .map_err(|e| format!("failed to execute service action: {e}"))?;

    if output.status.success() {
        return Ok("Service action completed.".to_string());
    }

    let err = String::from_utf8_lossy(&output.stderr).trim().to_string();
    if err.is_empty() {
        return Err("Service action failed. Administrator rights may be required.".to_string());
    }
    Err(err)
}

#[cfg(not(target_os = "windows"))]
async fn run_service_action(_name: &str, _action: &str) -> Result<String, String> {
    Err("Service actions are only supported on Windows.".to_string())
}
