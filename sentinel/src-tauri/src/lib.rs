mod commands;
mod ipc;
mod state;

use state::AppState;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let app_state = AppState::initialize().expect("failed to initialize Sentinel app state");

    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_fs::init())
        .manage(app_state)
        .invoke_handler(ipc::handler())
        .run(tauri::generate_context!())
        .expect("error while running Sentinel");
}
