using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;

namespace Sentinel.App.ViewModels;

public partial class ToolsViewModel : ObservableObject
{
    private readonly IToolsRunner _runner;

    public ToolsViewModel(IToolsRunner runner)
    {
        _runner = runner;
    }

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task TempCleanupAsync()
    {
        IsBusy = true;
        StatusText = "Cleaning temp files...";
        try
        {
            var r = await _runner.RunTempCleanupAsync();
            StatusText = r.Message;
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task UsbPowerDisableAsync()
    {
        IsBusy = true;
        StatusText = "Disabling USB power-saving...";
        try
        {
            var r = await _runner.RunUsbPowerDisableAsync();
            StatusText = r.Message;
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task TpmFixAsync()
    {
        IsBusy = true;
        StatusText = "Checking TPM attestation...";
        try
        {
            var r = await _runner.RunTpmAttestationFixAsync();
            StatusText = r.Message;
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private void RunZeroTrace()
    {
        var nav = App.Services?.GetService(typeof(INavigationService)) as INavigationService;
        nav?.NavigateTo("ZeroTrace", new ZeroTraceNavParams(LiveCleanup: true, FullCleanup: true));
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task RunWin11ReclaimAsync()
    {
        IsBusy = true;
        StatusText = "Launching Win11Reclaim...";
        try
        {
            var r = await _runner.RunWin11ReclaimAsync();
            StatusText = r.Message;
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool NotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        TempCleanupCommand.NotifyCanExecuteChanged();
        UsbPowerDisableCommand.NotifyCanExecuteChanged();
        TpmFixCommand.NotifyCanExecuteChanged();
        RunZeroTraceCommand.NotifyCanExecuteChanged();
        RunWin11ReclaimCommand.NotifyCanExecuteChanged();
    }
}