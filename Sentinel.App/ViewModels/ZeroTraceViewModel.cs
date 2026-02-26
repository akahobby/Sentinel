using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sentinel.Core.Interfaces;

namespace Sentinel.App.ViewModels;

/// <summary>Parameter passed when navigating to ZeroTrace page.</summary>
public sealed record ZeroTraceNavParams(bool LiveCleanup, bool FullCleanup = true);

/// <summary>Wrapper for a ZeroTrace target with include/exclude checkbox.</summary>
public sealed partial class ZeroTraceTargetItem : ObservableObject
{
    public ZeroTraceTarget Target { get; }

    [ObservableProperty] private bool _isIncluded = true;

    public string KindSourceConfidence => $"{Target.Kind} · {Target.Source} · {Target.Confidence}";
    public string Value => Target.Value;
    public string? Meta => Target.Meta;

    public ZeroTraceTargetItem(ZeroTraceTarget target) => Target = target;
}

public sealed partial class ZeroTraceViewModel : ObservableObject
{
    private readonly IToolsRunner _runner;

    public ZeroTraceViewModel(IToolsRunner runner)
    {
        _runner = runner;
    }

    [ObservableProperty] private bool _liveCleanup;
    [ObservableProperty] private bool _fullCleanup = true;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _hideMicrosoftApps = true;
    [ObservableProperty] private string _sortBy = "Name";
    [ObservableProperty] private ZeroTraceApp? _selectedApp;

    public IReadOnlyList<string> SortOptions { get; } = new[] { "Name", "Size" };

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ZeroTraceApp> Apps { get; } = new();
    public ObservableCollection<ZeroTraceTargetItem> TargetItems { get; } = new();

    private List<ZeroTraceApp> _allApps = new();
    private ZeroTraceApp? _scannedApp;

    public string StepTitle =>
        Step == ZeroTraceStep.AppPicker ? "Select an app" :
        Step == ZeroTraceStep.Scanning ? "Scanning..." :
        Step == ZeroTraceStep.Audit ? "Review targets" :
        "Done";

    /// <summary>True when Proceed button should be enabled (Audit step and not busy).</summary>
    public bool IsProceedEnabled => Step == ZeroTraceStep.Audit && !IsBusy;

    /// <summary>True when the Uninstall button should show in the toolbar (app selected on app list step).</summary>
    public bool ShowUninstallInToolbar => Step == ZeroTraceStep.AppPicker && SelectedApp != null;

    [ObservableProperty] private ZeroTraceStep _step = ZeroTraceStep.AppPicker;

    public enum ZeroTraceStep { AppPicker, Scanning, Audit, Done }

    partial void OnSearchTextChanged(string value)
    {
        ApplyAppFilter();
    }

    partial void OnHideMicrosoftAppsChanged(bool value)
    {
        ApplyAppFilter();
    }

    partial void OnSortByChanged(string value)
    {
        ApplyAppFilter();
    }

    private static bool IsMicrosoftApp(ZeroTraceApp app)
    {
        if (!string.IsNullOrWhiteSpace(app.Publisher) && app.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private void ApplyAppFilter()
    {
        Apps.Clear();
        var filtered = HideMicrosoftApps
            ? _allApps.Where(a => !IsMicrosoftApp(a)).ToList()
            : _allApps.ToList();
        var q = SearchText.Trim();
        var list = string.IsNullOrWhiteSpace(q)
            ? filtered
            : filtered.Where(a =>
                (a.DisplayName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Publisher?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        var ordered = SortBy == "Size"
            ? list.OrderByDescending(a => a.SizeBytes ?? 0).ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).Take(200).ToList()
            : list.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).Take(200).ToList();
        foreach (var a in ordered) Apps.Add(a);
    }

    partial void OnStepChanged(ZeroTraceStep value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(IsProceedEnabled));
        OnPropertyChanged(nameof(ShowUninstallInToolbar));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsProceedEnabled));
    }

    partial void OnSelectedAppChanged(ZeroTraceApp? value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowUninstallInToolbar));
    }

    [RelayCommand]
    private async Task LoadAppsAsync()
    {
        IsBusy = true;
        StatusText = "Loading installed apps...";
        try
        {
            _allApps = (await _runner.GetInstalledAppsForZeroTraceAsync(cancellationToken: default, skipSizeCalculation: false)).ToList();
            ApplyAppFilter();
            var count = HideMicrosoftApps ? _allApps.Count(a => !IsMicrosoftApp(a)) : _allApps.Count;
            StatusText = count + " app(s) shown. Select an app and click Uninstall to run its uninstaller (if any) and then scan for leftovers to delete." + (HideMicrosoftApps ? " (Microsoft apps hidden)" : "");
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

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (SelectedApp == null) return;
        _scannedApp = SelectedApp;
        Step = ZeroTraceStep.Scanning;
        IsBusy = true;
        StatusText = "Scanning for residuals...";
        try
        {
            var targets = await _runner.ScanZeroTraceAsync(SelectedApp, FullCleanup);
            TargetItems.Clear();
            foreach (var t in targets)
                TargetItems.Add(new ZeroTraceTargetItem(t));
            Step = ZeroTraceStep.Audit;
            var toRemove = TargetItems.Count(x => x.IsIncluded && x.Target.Exists && !x.Target.Blocked);
            StatusText = $"{targets.Count} item(s) found (files, registry, services, tasks). Select which to delete, then click Delete.";
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
            Step = ZeroTraceStep.Audit;
        }
        finally
        {
            IsBusy = false;
            ScanCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanScan() => SelectedApp != null && !IsBusy;

    private bool CanUninstall() => SelectedApp != null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallAsync()
    {
        if (SelectedApp == null) return;
        IsBusy = true;
        var hasUninstaller = !string.IsNullOrWhiteSpace(SelectedApp.UninstallString) || !string.IsNullOrWhiteSpace(SelectedApp.QuietUninstallString);
        if (hasUninstaller)
            StatusText = "Running uninstaller for " + SelectedApp.DisplayName + "...";
        else
            StatusText = "Scanning for leftover files and registry entries for " + SelectedApp.DisplayName + "...";
        try
        {
            if (hasUninstaller)
            {
                var result = await _runner.RunUninstallAsync(SelectedApp);
                StatusText = result.Message;
                if (!result.Success)
                {
                    IsBusy = false;
                    UninstallCommand.NotifyCanExecuteChanged();
                    return;
                }
                StatusText = "Uninstaller finished. Scanning for leftover files and registry entries...";
            }
            await ScanAsync();
        }
        catch (Exception ex)
        {
            StatusText = "Error: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            UninstallCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task RunCleanupAsync()
    {
        var toRemove = TargetItems.Where(x => x.IsIncluded && x.Target.Exists).Select(x => x.Target).ToList();
        if (toRemove.Count == 0)
        {
            // No selection or already empty — return to app list anyway so user isn't stuck
            Step = ZeroTraceStep.AppPicker;
            TargetItems.Clear();
            SelectedApp = null;
            StatusText = "No targets selected. Returned to app list.";
            await LoadAppsCommand.ExecuteAsync(null);
            return;
        }
        IsBusy = true;
        StatusText = "Removing selected targets...";
        try
        {
            var result = await _runner.RunZeroTraceCleanupAsync(toRemove);
            StatusText = result.Message;
            Step = ZeroTraceStep.AppPicker;
            TargetItems.Clear();
            SelectedApp = null;
            await LoadAppsCommand.ExecuteAsync(null);
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

    [RelayCommand]
    private void FinishScanOnly()
    {
        Step = ZeroTraceStep.Done;
        StatusText = "Scan complete. No changes were made (scan-only mode).";
    }

    [RelayCommand]
    private void SelectAllTargets()
    {
        foreach (var item in TargetItems)
            item.IsIncluded = true;
    }

    [RelayCommand]
    private void DeselectAllTargets()
    {
        foreach (var item in TargetItems)
            item.IsIncluded = false;
    }
}
