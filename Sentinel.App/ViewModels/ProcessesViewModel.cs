using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.App.ViewModels;

public partial class ProcessesViewModel : ObservableObject, IRefreshable
{
    private readonly IProcessCollector _collector;
    private DispatcherQueueTimer? _liveTimer;

    [ObservableProperty] private ObservableCollection<ProcessInfo> _processes = new();
    [ObservableProperty] private ObservableCollection<ProcessRowViewModel> _filteredRows = new();
    [ObservableProperty] private ProcessInfo? _selectedProcess;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _sortBy = "CPU";

    /// <summary>Section expanded state: Apps, Background, Windows. Expanded by default.</summary>
    [ObservableProperty] private bool _isAppsSectionExpanded = true;
    [ObservableProperty] private bool _isBackgroundSectionExpanded = true;
    [ObservableProperty] private bool _isWindowsSectionExpanded = true;

    public IReadOnlyList<string> SortOptions { get; } = new[] { "CPU", "Memory", "GPU", "Network", "Name" };

    private HashSet<int> _appPids = new();

    public ProcessesViewModel(IProcessCollector collector)
    {
        _collector = collector;
        if (DispatcherHelper.AppDispatcher != null)
        {
            _liveTimer = DispatcherHelper.AppDispatcher.CreateTimer();
            _liveTimer.Interval = TimeSpan.FromSeconds(1);
            _liveTimer.Tick += OnLiveTick;
            if (!LiveReadingsState.IsPaused)
                _liveTimer.Start();
        }
        LiveReadingsState.PausedChanged += OnPausedChanged;
    }

    private void OnPausedChanged()
    {
        if (_liveTimer == null) return;
        if (LiveReadingsState.IsPaused)
            _liveTimer.Stop();
        else
            _liveTimer.Start();
    }

    private async void OnLiveTick(DispatcherQueueTimer sender, object args)
    {
        if (LiveReadingsState.IsPaused || IsRefreshing) return;
        await RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            var appPidsTask = _collector.GetProcessIdsWithVisibleWindowsAsync();
            var list = await Task.Run(async () =>
            {
                var result = new List<ProcessInfo>();
                await foreach (var p in _collector.EnumerateAsync())
                    result.Add(p);
                return result;
            }).ConfigureAwait(true);
            var appSet = await appPidsTask.ConfigureAwait(true);
            _appPids = new HashSet<int>(appSet);
            Processes = new ObservableCollection<ProcessInfo>(list);
            ApplyFilter();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSortByChanged(string value) => ApplyFilter();

    async Task IRefreshable.RefreshAsync() => await RefreshCommand.ExecuteAsync(null);

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? Processes.ToList()
            : Processes.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        var grouped = filtered
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var list = g.ToList();
                var first = list.First();
                var isApp = list.Any(p => _appPids.Contains(p.Pid));
                IOrderedEnumerable<ProcessInfo> ordered = SortBy switch
                {
                    "Memory" => g.OrderByDescending(p => p.MemoryMb),
                    "GPU" => g.OrderByDescending(p => p.GpuPercent),
                    "Network" => g.OrderByDescending(p => p.NetworkKbps),
                    "Name" => g.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
                    _ => g.OrderByDescending(p => p.CpuPercent)
                };
                return new ProcessGroupViewModel
                {
                    Name = g.Key,
                    IsExpanded = false,
                    IsWindows = ProcessClassification.IsWindowsProcess(first),
                    IsApp = isApp,
                    Children = new ObservableCollection<ProcessInfo>(ordered.ToList())
                };
            })
            .ToList();

        static int SectionOrder(ProcessGroupViewModel grp)
        {
            if (grp.IsApp) return 0;
            if (grp.IsWindows) return 2;
            return 1;
        }

        List<ProcessGroupViewModel> orderedGroups = SortBy switch
        {
            "Memory" => grouped.OrderBy(SectionOrder).ThenByDescending(g => g.AggregateMemoryMb).ToList(),
            "GPU" => grouped.OrderBy(SectionOrder).ThenByDescending(g => g.AggregateGpu).ToList(),
            "Network" => grouped.OrderBy(SectionOrder).ThenByDescending(g => g.AggregateNetworkKbps).ToList(),
            "Name" => grouped.OrderBy(SectionOrder).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => grouped.OrderBy(SectionOrder).ThenByDescending(g => g.AggregateCpu).ToList()
        };

        _groups = orderedGroups;
        BuildFlatRows();
    }

    [RelayCommand]
    private void ToggleExpand(ProcessGroupViewModel? group)
    {
        if (group == null) return;
        group.IsExpanded = !group.IsExpanded;
        BuildFlatRows();
    }

    [RelayCommand]
    private void ToggleSection(int sectionKind)
    {
        switch (sectionKind)
        {
            case 0: IsAppsSectionExpanded = !IsAppsSectionExpanded; break;
            case 1: IsBackgroundSectionExpanded = !IsBackgroundSectionExpanded; break;
            case 2: IsWindowsSectionExpanded = !IsWindowsSectionExpanded; break;
        }
        BuildFlatRows();
    }

    private List<ProcessGroupViewModel> _groups = new();

    private void BuildFlatRows()
    {
        var rows = new List<ProcessRowViewModel>();
        int lastSectionKind = -1;

        static string SectionTitle(int kind) => kind switch { 0 => "Apps", 1 => "Background processes", _ => "Windows processes" };
        bool IsSectionExpanded(int k) => k switch { 0 => IsAppsSectionExpanded, 1 => IsBackgroundSectionExpanded, _ => IsWindowsSectionExpanded };
        static int SectionOrder(ProcessGroupViewModel g) => g.IsApp ? 0 : g.IsWindows ? 2 : 1;

        foreach (var group in _groups)
        {
            if (group.Children.Count == 0) continue;
            int kind = SectionOrder(group);
            if (lastSectionKind != kind)
            {
                lastSectionKind = kind;
                rows.Add(new ProcessRowViewModel
                {
                    IsSectionHeader = true,
                    SectionTitle = SectionTitle(kind),
                    SectionKind = kind,
                    SectionIsExpanded = IsSectionExpanded(kind)
                });
            }
            if (!IsSectionExpanded(kind))
                continue;
            if (group.Children.Count == 1)
            {
                var p = group.Children[0];
                rows.Add(new ProcessRowViewModel
                {
                    IsGroupHeader = false,
                    DisplayName = p.Name,
                    Subtitle = $"PID {p.Pid}",
                    CpuPercent = p.CpuPercent,
                    MemoryMb = p.MemoryMb,
                    GpuPercent = p.GpuPercent,
                    NetworkKbps = p.NetworkKbps,
                    IndentLevel = 0,
                    Process = p
                });
            }
            else
            {
                rows.Add(new ProcessRowViewModel
                {
                    IsGroupHeader = true,
                    DisplayName = group.Name,
                    Subtitle = $"{group.Children.Count} processes",
                    CpuPercent = group.AggregateCpu,
                    MemoryMb = group.AggregateMemoryMb,
                    GpuPercent = group.AggregateGpu,
                    NetworkKbps = group.AggregateNetworkKbps,
                    IndentLevel = 0,
                    IsExpanded = group.IsExpanded,
                    Group = group
                });
                if (group.IsExpanded)
                {
                    foreach (var p in group.Children)
                    {
                        rows.Add(new ProcessRowViewModel
                        {
                            IsGroupHeader = false,
                            DisplayName = p.Name,
                            Subtitle = $"PID {p.Pid}",
                            CpuPercent = p.CpuPercent,
                            MemoryMb = p.MemoryMb,
                            GpuPercent = p.GpuPercent,
                            NetworkKbps = p.NetworkKbps,
                            IndentLevel = 1,
                            Process = p
                        });
                    }
                }
            }
        }

        FilteredRows = new ObservableCollection<ProcessRowViewModel>(rows);
    }
}
