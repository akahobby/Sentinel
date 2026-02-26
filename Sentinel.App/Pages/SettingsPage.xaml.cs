using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;

namespace Sentinel.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var exporter = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IReportExporter)) as Sentinel.Core.Interfaces.IReportExporter;
        if (exporter != null)
            DataContext = new SettingsViewModel(exporter);
    }
}
