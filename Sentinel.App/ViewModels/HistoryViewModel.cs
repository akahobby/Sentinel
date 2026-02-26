using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.App.ViewModels;

public partial class HistoryViewModel : ObservableObject, IRefreshable
{
    private readonly IStorageService _storage;

    [ObservableProperty] private ObservableCollection<HistoryEntry> _timeline = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private int _daysBack = 7;

    public IReadOnlyList<int> DaysBackOptions { get; } = new[] { 1, 7, 14, 30 };

    public HistoryViewModel(IStorageService storage)
    {
        _storage = storage;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusText = "Loading...";
        try
        {
            await _storage.InitializeAsync();
            var to = DateTime.UtcNow;
            var from = to.AddDays(-Math.Max(1, DaysBack));
            var spikes = await _storage.GetSpikeEventsAsync(from, to);
            var changes = await _storage.GetChangeEventsAsync(from, to);
            var list = new List<HistoryEntry>();
            foreach (var s in spikes.OrderByDescending(x => x.StartUtc))
                list.Add(new HistoryEntry
                {
                    Title = $"Spike: {s.Metric}",
                    Detail = $"{s.ProcessName ?? "System"} peak {FormatPeakValue(s.Metric, s.PeakValue)} ({s.DurationSeconds:F0}s)" + (s.PossibleLeak ? " [possible leak]" : ""),
                    When = s.StartUtc.ToLocalTime().ToString("g"),
                    WhenUtc = s.StartUtc
                });
            foreach (var c in changes.OrderByDescending(x => x.DetectedUtc))
                list.Add(new HistoryEntry
                {
                    Title = $"{c.Category}: {c.ChangeType}",
                    Detail = c.Name ?? c.Details ?? "",
                    When = c.DetectedUtc.ToLocalTime().ToString("g"),
                    WhenUtc = c.DetectedUtc
                });
            list = list.OrderByDescending(x => x.WhenUtc).ToList();
            Timeline = new ObservableCollection<HistoryEntry>(list);
            StatusText = list.Count == 0
                ? $"No events in the last {DaysBack} days. Run a scan to record events."
                : $"{Timeline.Count} event(s) in the last {DaysBack} days.";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    async Task IRefreshable.RefreshAsync() => await LoadCommand.ExecuteAsync(null);

    private static string FormatPeakValue(string metric, double value)
    {
        return metric switch
        {
            "Memory" => Sentinel.Core.Formatting.UsageFormat.MemoryMb(value),
            "Cpu" => Sentinel.Core.Formatting.UsageFormat.CpuPercent(value),
            _ => value.ToString("F2")
        };
    }
}
