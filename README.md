# Sentinel

Sentinel is a **Windows 11–native Task Manager replacement** built with C# .NET 8 and WinUI 3. It focuses on a clean, dark UI and practical tools for seeing what’s running, what starts with Windows, and fixing a few common system annoyances—without tweak spam or silent elevation.

## Safety model

- **No silent elevation** – Anything that needs admin clearly asks you to relaunch as admin first.
- **Scoped changes only** – Startup and services changes are limited to obvious, reversible actions.
- **No “optimizer” nonsense** – No disabling security features or doing destructive registry hacks.

## Features

- **Overview** – Live CPU / RAM / GPU / network graphs and system snapshot (OS, CPU, GPU, RAM, uptime).
- **Processes** – Grouped process list with CPU / memory / GPU / network usage, search + sort, and a details pane.
- **Startup** – Enable/disable startup apps from Run keys and Startup folder, with impact hints.
- **Services** – Modern view over Windows services with safe start/stop and start‑type controls.
- **Tools** – Temp cleanup, USB power fix, TPM attestation repair, ZeroTrace deep uninstall, and Win11Reclaim launcher.
- **Premium UX** – Dark, clean UI with consistent spacing and typography.

## Download (GitHub Releases)

### Portable ZIP (no installer)

The recommended way to run Sentinel is from the prebuilt **portable ZIP** on the [GitHub Releases](https://github.com/akahobby/Sentinel/releases) page:

1. Download the latest `Sentinel-win-x64.zip` from the Releases tab.
2. Extract the ZIP to any folder (for example: `C:\Tools\Sentinel`).
3. Run `Sentinel.exe` from the extracted folder.

To remove Sentinel, just close it and delete the folder.

---

## Build from source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (x64)
- **Visual Studio 2022** with the **Windows App SDK / WinUI** workload for the `Sentinel.App` project.

### Steps

1. Clone the repo and restore/build the solution:

bash
git clone https://github.com/akahobby/Sentinel.git
cd Sentinel
dotnet restore Sentinel.sln
dotnet build Sentinel.sln -c Debug

2. Open `Sentinel.sln` in Visual Studio 2022.
3. Set `Sentinel.App` as the startup project.
4. Press **F5** (or **Ctrl+F5**) to run.

For your own local “portable” build you can also zip the `Sentinel.App\bin\Debug\net8.0-windows10.0.22621.0\win-x64\` (or `Release`) folder and run `Sentinel.exe` from the extracted copy, just like the GitHub portable ZIP.

## Solution structure

| Project             | Description |
|---------------------|-------------|
| `Sentinel.App`      | WinUI 3 UI (pages, view models, converters, styles, and infrastructure). |
| `Sentinel.Core`     | Models, interfaces, analyzers, storage, logging. |
| `Sentinel.Platform` | Windows‑specific collectors (processes, startup, services, boot) and trust verification. |
| `Sentinel.Tests`    | Unit tests for analyzers and parsing. |

## License

MIT.