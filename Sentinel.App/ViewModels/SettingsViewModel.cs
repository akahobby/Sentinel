using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Sentinel.Core.Interfaces;

namespace Sentinel.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IReportExporter _exporter;

    [ObservableProperty] private string _selectedTheme = "Default";
    [ObservableProperty] private double _samplingIntervalSeconds = 1;
    [ObservableProperty] private double _retentionDays = 7;
    [ObservableProperty] private bool _includeScheduledTasksInStartup;
    [ObservableProperty] private string _statusText = "";

    public SettingsViewModel(IReportExporter exporter)
    {
        _exporter = exporter;
    }

    public string[] Themes { get; } = { "Default", "Light", "Dark" };

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            var path = _exporter.GetLogsFolderPath();
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
            StatusText = "Opened logs folder.";
        }
        catch (Exception ex)
        {
            StatusText = "Failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RelaunchAsAdmin()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? "Sentinel.App.exe";
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(startInfo);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            StatusText = "Relaunch failed (user may have cancelled): " + ex.Message;
        }
    }
}
