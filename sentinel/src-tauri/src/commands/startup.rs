use crate::state::{AppState, ChangeEvent};
use chrono::Utc;
use serde::Serialize;
use tauri::State;
use tokio::task;

#[cfg(target_os = "windows")]
use crate::commands::trust;
#[cfg(target_os = "windows")]
use std::path::{Path, PathBuf};

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StartupItem {
    pub id: String,
    pub name: String,
    pub command: String,
    pub location: String,
    pub is_enabled: bool,
    pub path: Option<String>,
    pub signed: bool,
    pub publisher: Option<String>,
    pub risk: String,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StartupItemsResponse {
    pub items: Vec<StartupItem>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StartupToggleResponse {
    pub success: bool,
    pub id: String,
    pub enabled: bool,
    pub message: String,
}

#[tauri::command]
pub async fn list_startup_items() -> Result<StartupItemsResponse, String> {
    let items = task::spawn_blocking(list_startup_items_impl)
        .await
        .map_err(|e| format!("failed to list startup items: {e}"))??;
    Ok(StartupItemsResponse { items })
}

#[tauri::command]
pub async fn toggle_startup_item(
    id: String,
    enabled: bool,
    state: State<'_, AppState>,
) -> Result<StartupToggleResponse, String> {
    let id_for_task = id.clone();
    let result = task::spawn_blocking(move || toggle_startup_item_impl(&id_for_task, enabled))
        .await
        .map_err(|e| format!("failed to toggle startup item: {e}"))?;

    match result {
        Ok(message) => {
            let app_state = state.inner().clone();
            let id_clone = id.clone();
            let msg_clone = message.clone();
            let _ = task::spawn_blocking(move || {
                let _ = app_state.write_log_line(&format!(
                    "Startup item toggled id={} enabled={} message={}",
                    id_clone, enabled, msg_clone
                ));
                let _ = app_state.record_change_event(&ChangeEvent {
                    id: None,
                    detected_utc: Utc::now(),
                    category: "Startup".to_string(),
                    change_type: "Modified".to_string(),
                    name: Some(id_clone),
                    path: None,
                    details: Some(msg_clone),
                    is_approved: false,
                    is_ignored: false,
                });
            })
            .await;

            Ok(StartupToggleResponse {
                success: true,
                id,
                enabled,
                message,
            })
        }
        Err(err) => Ok(StartupToggleResponse {
            success: false,
            id,
            enabled,
            message: err,
        }),
    }
}

#[cfg(target_os = "windows")]
fn with_trust(mut item: StartupItem) -> StartupItem {
    let trust_meta = trust::quick_trust_from_path(item.path.as_deref());
    item.signed = trust_meta.signed;
    item.publisher = trust_meta.publisher;
    item.risk = trust::assess_risk(item.path.as_deref(), item.publisher.as_deref(), item.signed, Some(&item.name));
    item
}

#[cfg(target_os = "windows")]
fn extract_executable_path(command: &str) -> Option<String> {
    let trimmed = command.trim();
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
fn list_startup_items_impl() -> Result<Vec<StartupItem>, String> {
    use winreg::enums::*;
    use winreg::RegKey;

    let mut items = Vec::new();
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);

    let run_key = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    let startup_approved = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";
    let disabled_prefix = "_Sentinel_Disabled_";

    if let Ok(key) = hkcu.open_subkey(run_key) {
        let approved = hkcu.open_subkey(startup_approved).ok();
        for name in key.enum_values().flatten().map(|(n, _)| n) {
            if let Ok(command) = key.get_value::<String, _>(&name) {
                if name.starts_with(disabled_prefix) {
                    let original = name.trim_start_matches(disabled_prefix).to_string();
                    let item = StartupItem {
                        id: format!("HKCU Run:{original}"),
                        name: original,
                        command: command.clone(),
                        location: "HKCU Run".to_string(),
                        is_enabled: false,
                        path: extract_executable_path(&command),
                        signed: false,
                        publisher: None,
                        risk: "unknown".to_string(),
                    };
                    items.push(with_trust(item));
                } else {
                    let is_enabled = approved
                        .as_ref()
                        .and_then(|k| k.get_value::<Vec<u8>, _>(&name).ok())
                        .map(|x| x.first().copied().unwrap_or(0) != 0x03)
                        .unwrap_or(true);

                    let item = StartupItem {
                        id: format!("HKCU Run:{name}"),
                        name,
                        command: command.clone(),
                        location: "HKCU Run".to_string(),
                        is_enabled,
                        path: extract_executable_path(&command),
                        signed: false,
                        publisher: None,
                        risk: "unknown".to_string(),
                    };
                    items.push(with_trust(item));
                }
            }
        }
    }

    if let Ok(key) = hklm.open_subkey(run_key) {
        for name in key.enum_values().flatten().map(|(n, _)| n) {
            if let Ok(command) = key.get_value::<String, _>(&name) {
                let item = StartupItem {
                    id: format!("HKLM Run:{name}"),
                    name,
                    command: command.clone(),
                    location: "HKLM Run".to_string(),
                    is_enabled: true,
                    path: extract_executable_path(&command),
                    signed: false,
                    publisher: None,
                    risk: "unknown".to_string(),
                };
                items.push(with_trust(item));
            }
        }
    }

    if let Ok(appdata) = std::env::var("APPDATA") {
        let startup_dir = PathBuf::from(appdata).join("Microsoft/Windows/Start Menu/Programs/Startup");
        if startup_dir.exists() {
            for entry in std::fs::read_dir(&startup_dir).map_err(|e| e.to_string())? {
                let entry = entry.map_err(|e| e.to_string())?;
                let path = entry.path();
                if path.extension().is_some_and(|x| x.to_string_lossy().eq_ignore_ascii_case("lnk")) {
                    let display_name = path
                        .file_stem()
                        .map(|x| x.to_string_lossy().to_string())
                        .unwrap_or_else(|| "startup-item".to_string());
                    let full_path = path.to_string_lossy().to_string();
                    let item = StartupItem {
                        id: format!("folder:{full_path}"),
                        name: display_name,
                        command: full_path.clone(),
                        location: "Startup folder".to_string(),
                        is_enabled: true,
                        path: Some(full_path),
                        signed: false,
                        publisher: None,
                        risk: "unknown".to_string(),
                    };
                    items.push(with_trust(item));
                }
            }

            let disabled_dir = startup_dir.join("Disabled");
            if disabled_dir.exists() {
                for entry in std::fs::read_dir(&disabled_dir).map_err(|e| e.to_string())? {
                    let entry = entry.map_err(|e| e.to_string())?;
                    let path = entry.path();
                    if path.extension().is_some_and(|x| x.to_string_lossy().eq_ignore_ascii_case("lnk")) {
                        let display_name = path
                            .file_stem()
                            .map(|x| x.to_string_lossy().to_string())
                            .unwrap_or_else(|| "startup-item".to_string());
                        let full_path = path.to_string_lossy().to_string();
                        let item = StartupItem {
                            id: format!("folder:{full_path}"),
                            name: display_name,
                            command: full_path.clone(),
                            location: "Startup folder".to_string(),
                            is_enabled: false,
                            path: Some(full_path),
                            signed: false,
                            publisher: None,
                            risk: "unknown".to_string(),
                        };
                        items.push(with_trust(item));
                    }
                }
            }
        }
    }

    Ok(items)
}

#[cfg(not(target_os = "windows"))]
fn list_startup_items_impl() -> Result<Vec<StartupItem>, String> {
    Ok(Vec::new())
}

#[cfg(target_os = "windows")]
fn toggle_startup_item_impl(id: &str, enabled: bool) -> Result<String, String> {
    use winreg::enums::*;
    use winreg::RegKey;

    let run_key = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

    if let Some(name) = id.strip_prefix("HKCU Run:") {
        let hkcu = RegKey::predef(HKEY_CURRENT_USER);
        let key = hkcu
            .open_subkey_with_flags(run_key, KEY_READ | KEY_WRITE)
            .map_err(|e| format!("failed to open HKCU Run key: {e}"))?;

        if enabled {
            let backup_name = format!("_Sentinel_Disabled_{name}");
            let command: String = key
                .get_value(&backup_name)
                .map_err(|e| format!("failed to read backup value: {e}"))?;
            key.set_value(name, &command)
                .map_err(|e| format!("failed to restore startup item: {e}"))?;
            let _ = key.delete_value(&backup_name);
            return Ok("Startup item enabled.".to_string());
        }

        let command: String = key
            .get_value(name)
            .map_err(|e| format!("failed to read startup item: {e}"))?;
        let backup_name = format!("_Sentinel_Disabled_{name}");
        key.set_value(&backup_name, &command)
            .map_err(|e| format!("failed to store backup startup item: {e}"))?;
        let _ = key.delete_value(name);
        return Ok("Startup item disabled.".to_string());
    }

    if let Some(name) = id.strip_prefix("HKLM Run:") {
        let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
        let key = hklm
            .open_subkey_with_flags(run_key, KEY_READ | KEY_WRITE)
            .map_err(|e| format!("failed to open HKLM Run key (admin required): {e}"))?;

        if enabled {
            return Ok(format!("HKLM startup item '{name}' is already enabled."));
        }

        let _ = key.delete_value(name);
        return Ok("HKLM startup item disabled.".to_string());
    }

    if let Some(path) = id.strip_prefix("folder:") {
        let item_path = PathBuf::from(path);
        if enabled {
            if item_path.exists() {
                let parent = item_path.parent().unwrap_or_else(|| Path::new(""));
                if parent
                    .file_name()
                    .is_some_and(|x| x.to_string_lossy().eq_ignore_ascii_case("Disabled"))
                {
                    let startup_dir = parent.parent().ok_or("could not locate startup dir")?;
                    let file_name = item_path.file_name().ok_or("invalid startup file")?;
                    let dest = startup_dir.join(file_name);
                    std::fs::rename(&item_path, &dest)
                        .map_err(|e| format!("failed to enable startup folder item: {e}"))?;
                }
            }
            return Ok("Startup folder item enabled.".to_string());
        }

        if !item_path.exists() {
            return Err("Startup folder item not found.".to_string());
        }
        let parent = item_path.parent().ok_or("invalid startup folder item path")?;
        let disabled_dir = parent.join("Disabled");
        std::fs::create_dir_all(&disabled_dir).map_err(|e| format!("failed to create Disabled dir: {e}"))?;
        let file_name = item_path.file_name().ok_or("invalid startup file")?;
        let dest = disabled_dir.join(file_name);
        std::fs::rename(&item_path, &dest).map_err(|e| format!("failed to disable startup folder item: {e}"))?;
        return Ok("Startup folder item disabled.".to_string());
    }

    Err("Unsupported startup item id format.".to_string())
}

#[cfg(not(target_os = "windows"))]
fn toggle_startup_item_impl(_id: &str, _enabled: bool) -> Result<String, String> {
    Err("Startup item toggling is only supported on Windows.".to_string())
}
