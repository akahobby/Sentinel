use crate::commands;

pub fn handler<R: tauri::Runtime>() -> impl Fn(tauri::ipc::Invoke<R>) -> bool + Send + Sync + 'static {
    tauri::generate_handler![
        commands::process::list_processes,
        commands::process::get_process_details,
        commands::process::kill_process,
        commands::startup::list_startup_items,
        commands::startup::toggle_startup_item,
        commands::services::list_services,
        commands::services::service_action,
        commands::analyzer::analyze_system,
        commands::analyzer::get_spike_events,
        commands::analyzer::export_report
    ]
}
