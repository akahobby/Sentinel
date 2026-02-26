using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;

namespace Sentinel.App.Pages;

public sealed partial class OverviewPage : Page
{
    public OverviewPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var collector = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IProcessCollector)) as Sentinel.Core.Interfaces.IProcessCollector;
        var perf = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.ISystemPerformanceService)) as Sentinel.Core.Interfaces.ISystemPerformanceService;
        var systemInfo = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.ISystemInfoProvider)) as Sentinel.Core.Interfaces.ISystemInfoProvider;
        var servicesCollector = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IServicesCollector)) as Sentinel.Core.Interfaces.IServicesCollector;
        if (collector != null && perf != null)
            DataContext = new OverviewViewModel(collector, perf, systemInfo, servicesCollector);
    }
}
