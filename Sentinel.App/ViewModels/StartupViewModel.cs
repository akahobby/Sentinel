using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.App.ViewModels;

public partial class StartupViewModel : ObservableObject, IRefreshable
{
    private readonly IStartupCollector _collector;
    private DispatcherQueueTimer? _liveTimer;

    [ObservableProperty] private ObservableCollection<StartupItem> _items = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public StartupViewModel(IStartupCollector? collector)
    {
        _collector = collector ?? new EmptyStartupCollector();
        if (DispatcherHelper.AppDispatcher != null)
        {
            _liveTimer = DispatcherHelper.AppDispatcher.CreateTimer();
            _liveTimer.Interval = TimeSpan.FromSeconds(25);
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
        if (LiveReadingsState.IsPaused || IsLoading) return;
        await RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusText = "Loading...";
        try
        {
            var list = await _collector.GetAllAsync();
            Items = new ObservableCollection<StartupItem>(list);
            StatusText = $"{Items.Count} startup item(s).";
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

    [RelayCommand]
    private async Task ToggleEnabledAsync(StartupItem? item)
    {
        if (item == null) return;
        try
        {
            if (item.IsEnabled)
                await _collector.DisableAsync(item);
            else
                await _collector.EnableAsync(item);
            await RefreshAsync();
        }
        catch (Exception)
        {
            await RefreshAsync();
        }
    }

    async Task IRefreshable.RefreshAsync() => await RefreshCommand.ExecuteAsync(null);
}

internal sealed class EmptyStartupCollector : IStartupCollector
{
    public Task<IReadOnlyList<StartupItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<StartupItem>)new List<StartupItem>());
    public Task EnableAsync(StartupItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisableAsync(StartupItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
