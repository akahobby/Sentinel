# Sentinel (Tauri + React migration)

This folder contains the migrated Sentinel desktop application:

- `frontend/`: Vite + React + TypeScript UI
- `src-tauri/`: Tauri shell + Rust native backend commands

## Implemented command surface

The backend exposes async Tauri commands:

- `list_processes`
- `get_process_details`
- `kill_process`
- `list_startup_items`
- `toggle_startup_item`
- `list_services`
- `service_action`
- `analyze_system`
- `get_spike_events`
- `export_report`

## Storage and logs

- Logs: `%LOCALAPPDATA%/Sentinel/logs/`
- Reports: `%LOCALAPPDATA%/Sentinel/reports/`
- Exports: `%LOCALAPPDATA%/Sentinel/exports/`
- SQLite history DB: `%LOCALAPPDATA%/Sentinel/Data/sentinel.db`

## Development

```bash
cd sentinel/frontend
npm install

cd ../src-tauri
cargo check
```

Run app in development mode:

```bash
cd sentinel/frontend
npm run tauri dev
```

## Portable Windows build

Build frontend + Tauri app target:

```bash
cd sentinel/frontend
npm run tauri build
```

The portable output is generated under `src-tauri/target/release/bundle/app/`.
Zip the folder contents (including `sentinel.exe` and runtime files) for distribution.
