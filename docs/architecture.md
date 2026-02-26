# Sentinel Architecture

## Overview

Sentinel is a Windows 11–native Task Manager replacement built with C# .NET 8, WinUI 3 (Windows App SDK), and MVVM.

## Flow: Collector → Storage → UI

```
┌─────────────────────────────────────────────────────────────────┐
│  Sentinel.App (WinUI 3)                                          │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────┐  │
│  │ Pages       │  │ ViewModels   │  │ Infrastructure           │  │
│  │ Overview,   │  │ bind to      │  │ NavigationService,       │  │
│  │ Processes,  │  │ observables  │  │ AsyncCommand, Debouncer  │  │
│  │ Startup,    │  │ and commands │  │ DispatcherHelper         │  │
│  │ Analysis,   │  └──────┬───────┘  └─────────────────────────┘  │
│  │ History,    │         │                                        │
│  │ Services,   │         ▼                                        │
│  │ Reports,    │  ┌──────────────┐                               │
│  │ Settings    │  │ Design System │  Theme, Typography, Brushes    │
│  └─────────────┘  └───────────────┘                               │
└────────────────────────────┬────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Sentinel.Core                                                    │
│  Models (ProcessInfo, StartupItem, ServiceInfo, ProcessSample,   │
│          SpikeEvent, ChangeEvent, AnalyzerFinding, LatestReport) │
│  Interfaces (IProcessCollector, IStartupCollector,               │
│              IServicesCollector, ITrustVerifier, IAnalyzerService,│
│              IStorageService, IReportExporter, IBootTracker)     │
│  Storage (StorageService – SQLite), Export (ReportExporter),     │
│  Analysis (AnalyzerService), Logging (AppLog, LoggingSetup)     │
└────────────────────────────┬────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Sentinel.Platform (Windows-specific)                             │
│  Collectors: ProcessCollector, StartupCollector, ServicesCollector│
│  Trust: TrustVerifier (signature, SHA-256, risk)                 │
│  BootTracker (WMI LastBootUpTime)                               │
└─────────────────────────────────────────────────────────────────┘
```

## Data flow

- **Process list**: `IProcessCollector.EnumerateAsync()` → UI table (diff updates by PID).
- **Samples**: Background loop writes `ProcessSample` to `IStorageService`; retention applies.
- **Spikes / changes**: Detected by platform or analyzer → `WriteSpikeEventAsync` / `WriteChangeEventAsync`.
- **Reports**: `IReportExporter.WriteLatestJsonAsync` and `ExportZipAsync` use paths under `%LOCALAPPDATA%\Sentinel\`.

## Key design decisions

- **No silent admin**: Admin actions show “Requires admin” and “Relaunch as Admin” instead of elevating silently.
- **Reversible changes**: Startup enable/disable and service start-type changes store revert data where possible.
- **Performance**: Process table keyed by PID; only changed rows updated; UI refresh throttled; signature/hash/icon cached off UI thread.
