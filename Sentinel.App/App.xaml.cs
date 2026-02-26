using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Sentinel.App.Infrastructure;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Storage;
using Sentinel.Core.Export;
using Sentinel.Core.Analysis;
using Sentinel.Platform.Collectors;
using Sentinel.Platform.Trust;

namespace Sentinel.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    public static IServiceProvider? Services { get; private set; }

    public App()
    {
        InitializeComponent();
        Core.Logging.LoggingSetup.Initialize();
        var sc = new ServiceCollection();
        sc.AddSingleton<INavigationService, NavigationService>();
        sc.AddSingleton<IProcessCollector, ProcessCollector>();
        sc.AddSingleton<IStartupCollector, StartupCollector>();
        sc.AddSingleton<IServicesCollector, ServicesCollector>();
        sc.AddSingleton<ITrustVerifier, TrustVerifier>();
        sc.AddSingleton<IBootTracker, BootTracker>();
        sc.AddSingleton<IStorageService>(_ => new StorageService(7, 30));
        sc.AddSingleton<ISystemPerformanceService, SystemPerformanceService>();
        sc.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        sc.AddSingleton<IToolsRunner, ToolsRunner>();
        Services = sc.BuildServiceProvider();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Core.Logging.AppLog.Fatal(e.Exception, "Unhandled exception");
        e.Handled = true;
    }
}
