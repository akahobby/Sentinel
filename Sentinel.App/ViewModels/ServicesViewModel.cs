using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.App.ViewModels;

public partial class ServicesViewModel : ObservableObject, IRefreshable
{
    private readonly IServicesCollector _collector;
    private DispatcherQueueTimer? _liveTimer;

    [ObservableProperty] private ObservableCollection<ServiceInfo> _services = new();
    [ObservableProperty] private ServiceInfo? _selectedService;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ServicesViewModel(IServicesCollector collector)
    {
        _collector = collector;
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
            Services = new ObservableCollection<ServiceInfo>(list);
            StatusText = $"{Services.Count} service(s).";
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

    [RelayCommand(CanExecute = nameof(CanRunServiceAction))]
    private async Task StartServiceAsync(ServiceInfo? service)
    {
        if (service == null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _collector.StartAsync(service.Name);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = "Start failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunServiceAction))]
    private async Task StopServiceAsync(ServiceInfo? service)
    {
        if (service == null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _collector.StopAsync(service.Name);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = "Stop failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunServiceAction))]
    private async Task SetStartTypeAsync(ServiceInfo? service)
    {
        if (service == null || IsBusy) return;
        // Cycle or show dialog: Automatic -> Manual -> Disabled -> Automatic
        var next = service.StartType?.ToLowerInvariant() switch
        {
            "automatic" => "Manual",
            "manual" => "Disabled",
            "disabled" => "Automatic",
            _ => "Manual"
        };
        IsBusy = true;
        try
        {
            await _collector.SetStartTypeAsync(service.Name, next);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunServiceAction(ServiceInfo? service) => service != null && !IsBusy;

    partial void OnSelectedServiceChanged(ServiceInfo? value)
    {
        StartServiceCommand.NotifyCanExecuteChanged();
        StopServiceCommand.NotifyCanExecuteChanged();
        SetStartTypeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        StartServiceCommand.NotifyCanExecuteChanged();
        StopServiceCommand.NotifyCanExecuteChanged();
        SetStartTypeCommand.NotifyCanExecuteChanged();
    }

    async Task IRefreshable.RefreshAsync() => await RefreshCommand.ExecuteAsync(null);
}
