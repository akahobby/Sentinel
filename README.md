# Sentinel

A premium **Windows 11–native Task Manager replacement** built with C# .NET 8, WinUI 3 (Windows App SDK), and MVVM. Sentinel provides process exploration, startup impact analysis, “why is my PC slow?” diagnostics, history and spike logging, change detection, services management, and reports—with a Fluent/Mica-style UI and no silent admin elevation.

## Safety model

- **No silent elevation**: Admin actions show a “Requires admin” badge and an explicit “Relaunch as Admin” action. The app never elevates without user consent.
- **Reversible changes**: Startup enable/disable and service start-type changes store revert data where possible so you can undo.
- **No “tweak spam”**: No disabling security features or installing drivers; no shady or destructive recommendations.

## Features

- **Process Explorer** – Modern table (CPU, memory, disk, network), tree view, details pane, trust/signature verification, smart actions (end task, open file location, etc.).
- **Startup Impact** – Enable/disable startup items, impact (Low/Med/High), publisher/signature, command line; Run keys, Startup folder, optional scheduled tasks.
- **“Why is my PC slow?” Analyzer** – CPU/RAM/disk/network diagnosis with explainable findings and actionable recommendations.
- **History & Spike Logger** – Time-series samples, per-process history, event timeline; configurable retention.
- **Change Detection** – Startup/service/task/background changes with approve/ignore to reduce noise.
- **Services Manager** – Clean UI for Windows services with descriptions and safe start/stop/start-type controls.
- **Reports & Export** – JSON report (`latest.json`), ZIP export, copy diagnostics.
- **Premium UX** – Mica-backed UI, consistent spacing and typography, animations, empty states.

## Where logs and reports live

- **Daily log**: `%LOCALAPPDATA%\Sentinel\logs\YYYY-MM-DD.log`
- **JSON report**: `%LOCALAPPDATA%\Sentinel\reports\latest.json`
- **Export ZIP**: `%LOCALAPPDATA%\Sentinel\exports\Sentinel_Report_YYYY-MM-DD_HH-mm.zip`
- **SQLite DB**: `%LOCALAPPDATA%\Sentinel\Data\sentinel.db`

## Build from source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (x64)
- For **Sentinel.App** (WinUI 3): **Visual Studio 2022** with workload **“Windows App SDK”** / **“WinUI”** (or equivalent) so the XAML compiler runs correctly. The Core, Platform, and Tests projects build with `dotnet build`; the App project may require the full VS WinUI toolchain.

### Steps

1. Clone the repo and open a terminal in the solution root.
2. Restore and build (Core, Platform, Tests):
   ```bash
   dotnet restore Sentinel.sln
   dotnet build Sentinel.sln -c Debug
   ```
3. Build/pack the WinUI app (prefer doing this from **Visual Studio 2022** with the WinUI workload):
   - Open `Sentinel.sln` in Visual Studio 2022.
   - Set **Sentinel.App** as the startup project.
   - Build and run (F5).
4. Or use the helper script (builds solution; packaging is optional):
   ```powershell
   .\scripts\build.ps1 -Configuration Debug
   .\scripts\build.ps1 -Configuration Release -Pack    # MSIX
   .\scripts\build.ps1 -Configuration Release -Portable # Unpackaged portable
   ```

### Packaging

- **MSIX (recommended)**: Use `-Pack` with `build.ps1` or publish from Visual Studio with the Windows Application Packaging Project / single-project MSIX. Output is under `Sentinel.App\bin\...\publish\`.
- **Unpackaged portable**: Use `-Portable` or publish with `WindowsPackageType=None`. Run the published exe from the output folder; no install required.

## Solution structure

| Project         | Description |
|-----------------|-------------|
| **Sentinel.App**   | WinUI 3 UI (Pages, ViewModels, Controls, Resources, Infrastructure). |
| **Sentinel.Core**  | Models, interfaces, analyzers, SQLite storage, export, logging. |
| **Sentinel.Platform** | Windows collectors (processes, startup, services, boot) and trust verification. |
| **Sentinel.Tests** | Unit tests for analyzers and parsing. |

See **docs/architecture.md**, **docs/ui-styleguide.md**, and **docs/data-schema.md** for details.

## Screenshots

*(Placeholder: add screenshots of Overview, Processes, and Analysis pages here.)*

## License

MIT (or your chosen license).
