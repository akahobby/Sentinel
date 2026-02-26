using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.App.ViewModels;

public partial class OverviewViewModel : ObservableObject, IRefreshable
{
    private readonly IProcessCollector _processCollector;
    private readonly ISystemPerformanceService _perf;
    private readonly ISystemInfoProvider? _systemInfo;
    private readonly IServicesCollector? _servicesCollector;
    private DispatcherQueueTimer? _perfTimer;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private ObservableCollection<AnalyzerFinding> _findings = new();
    [ObservableProperty] private ObservableCollection<ProcessInfo> _topCpu = new();
    [ObservableProperty] private ObservableCollection<ProcessInfo> _topMemory = new();
    [ObservableProperty] private ObservableCollection<ProcessInfo> _topGpu = new();
    [ObservableProperty] private ObservableCollection<ProcessInfo> _topNetwork = new();
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private double _liveCpuPercent;
    [ObservableProperty] private double _liveMemoryPercent;
    [ObservableProperty] private double _liveGpuPercent;
    [ObservableProperty] private double _liveNetworkKbps;
    [ObservableProperty] private ObservableCollection<double> _cpuGraph = new();
    [ObservableProperty] private ObservableCollection<double> _memoryGraph = new();
    [ObservableProperty] private ObservableCollection<double> _cpuGraphPixels = new();
    [ObservableProperty] private ObservableCollection<double> _memoryGraphPixels = new();
    [ObservableProperty] private ObservableCollection<double> _gpuGraphPixels = new();
    [ObservableProperty] private ObservableCollection<double> _networkGraphPixels = new();

    // Wintoys-style dashboard
    [ObservableProperty] private string _systemName = "—";
    [ObservableProperty] private string _processorName = "—";
    [ObservableProperty] private string _graphicsName = "—";
    [ObservableProperty] private string _memoryGb = "—";
    [ObservableProperty] private string _storageGb = "—";
    [ObservableProperty] private string _windowsVersion = "—";
    [ObservableProperty] private string _upTimeFormatted = "—";
    [ObservableProperty] private int _appsCount;
    [ObservableProperty] private int _processesCount;
    [ObservableProperty] private int _servicesCount;

    public OverviewViewModel(IProcessCollector processCollector, ISystemPerformanceService perf,
        ISystemInfoProvider? systemInfo = null, IServicesCollector? servicesCollector = null)
    {
        _processCollector = processCollector;
        _perf = perf;
        _systemInfo = systemInfo;
        _servicesCollector = servicesCollector;
        if (DispatcherHelper.AppDispatcher != null)
        {
            _perfTimer = DispatcherHelper.AppDispatcher.CreateTimer();
            _perfTimer.Interval = TimeSpan.FromSeconds(1);
            _perfTimer.Tick += OnPerfTick;
            if (!LiveReadingsState.IsPaused)
                _perfTimer.Start();
        }
        LiveReadingsState.PausedChanged += OnPausedChanged;
        _ = LoadDashboardAsync();
    }

    private void OnPausedChanged()
    {
        if (_perfTimer == null) return;
        if (LiveReadingsState.IsPaused)
            _perfTimer.Stop();
        else
            _perfTimer.Start();
    }

    private async Task LoadDashboardAsync()
    {
        if (_systemInfo != null)
        {
            try
            {
                var info = await _systemInfo.GetSystemInfoAsync();
                SystemName = info.SystemName;
                ProcessorName = info.ProcessorName;
                GraphicsName = info.GraphicsName;
                MemoryGb = info.MemoryGb;
                StorageGb = info.StorageGb;
                WindowsVersion = info.WindowsVersion;
                UpTimeFormatted = info.UpTimeFormatted;
            }
            catch { }
        }
        try
        {
            var appPids = await _processCollector.GetProcessIdsWithVisibleWindowsAsync();
            AppsCount = appPids.Count;
        }
        catch { }
        try
        {
            var count = await Task.Run(() => System.Diagnostics.Process.GetProcesses().Length);
            ProcessesCount = count;
        }
        catch { }
        if (_servicesCollector != null)
        {
            // Load services count in background so it doesn't block UI (GetAllAsync is slow)
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                try
                {
                    var list = await _servicesCollector.GetAllAsync();
                    if (DispatcherHelper.AppDispatcher != null)
                        DispatcherHelper.RunOnUiThread(() => ServicesCount = list.Count);
                }
                catch { }
            });
        }
    }

    private async void OnPerfTick(DispatcherQueueTimer sender, object args)
    {
        if (LiveReadingsState.IsPaused) return;
        try
        {
            await Task.Run(async () => await _perf.SampleAsync()).ConfigureAwait(true);
            LiveCpuPercent = _perf.CpuPercent;
            LiveMemoryPercent = _perf.MemoryPercent;
            LiveGpuPercent = _perf.GpuPercent;
            LiveNetworkKbps = _perf.NetworkKbps;
            UpdateGraphPixelsInPlace();
        }
        catch { }
    }

    private void UpdateGraphPixelsInPlace()
    {
        const double networkMaxKbps = 500;
        var cpuPixels = _perf.CpuHistory.Select(x => Math.Max(2, x / 100.0 * 48)).ToList();
        var memPixels = _perf.MemoryHistory.Select(x => Math.Max(2, x / 100.0 * 48)).ToList();
        var gpuPixels = _perf.GpuHistory.Select(x => Math.Max(2, x / 100.0 * 48)).ToList();
        var netPixels = _perf.NetworkHistory.Select(k => Math.Max(2, Math.Min(48, k / networkMaxKbps * 48))).ToList();
        CpuGraphPixels.Clear();
        foreach (var p in cpuPixels) CpuGraphPixels.Add(p);
        MemoryGraphPixels.Clear();
        foreach (var p in memPixels) MemoryGraphPixels.Add(p);
        GpuGraphPixels.Clear();
        foreach (var p in gpuPixels) GpuGraphPixels.Add(p);
        NetworkGraphPixels.Clear();
        foreach (var p in netPixels) NetworkGraphPixels.Add(p);
    }

    [RelayCommand]
    private async Task RunScanAsync()
    {
        // Scan functionality removed from UI; keep command as no-op for compatibility.
        await Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        // No-op: findings scan removed; overview is now purely live data.
        await Task.CompletedTask;
    }
}
