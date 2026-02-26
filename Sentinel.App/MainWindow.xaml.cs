using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Dispatching;
using Sentinel.App.Infrastructure;

namespace Sentinel.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private DispatcherQueueTimer? _ctrlKeyTimer;

    public MainWindow()
    {
        InitializeComponent();
        DispatcherHelper.AppDispatcher = DispatcherQueue;
        _navigation = (INavigationService?)App.Services?.GetService(typeof(INavigationService)) ?? new NavigationService();
        _navigation.SetFrame(ContentFrame);
        RegisterPages();
        NavView.ItemInvoked += OnItemInvoked;
        NavView.BackRequested += (_, _) => NavView.IsBackEnabled = false;
        PauseButton.Click += OnPauseClick;
        LiveReadingsState.PausedChanged += UpdatePauseButtonState;
        if (Content is Microsoft.UI.Xaml.FrameworkElement root)
            root.Loaded += OnLoaded;
        else
            OnLoaded(this, null!);
    }

    private void StartCtrlKeyPolling()
    {
        _ctrlKeyTimer ??= DispatcherQueue.CreateTimer();
        _ctrlKeyTimer.Interval = TimeSpan.FromMilliseconds(150);
        _ctrlKeyTimer.Tick += (_, _) => LiveReadingsState.UpdateCtrlKeyState();
        _ctrlKeyTimer.Start();
    }

    private void RegisterPages()
    {
        _navigation.Register("Overview", typeof(Pages.OverviewPage));
        _navigation.Register("Processes", typeof(Pages.ProcessesPage));
        _navigation.Register("Startup", typeof(Pages.StartupPage));
        _navigation.Register("Services", typeof(Pages.ServicesPage));
        _navigation.Register("Tools", typeof(Pages.ToolsPage));
        _navigation.Register("ZeroTrace", typeof(Pages.ZeroTracePage));
        _navigation.Register("Settings", typeof(Pages.SettingsPage));
    }

    private void OnLoaded(object sender, RoutedEventArgs? e)
    {
        _navigation.NavigateTo("Overview");
        UpdateAdminBadge();
        UpdatePauseButtonState();
        StartCtrlKeyPolling();
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _navigation.NavigateTo("Settings");
            return;
        }
        if (args.InvokedItemContainer?.Tag is string tag)
            _navigation.NavigateTo(tag);
    }

    private void UpdateAdminBadge()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            AdminBadgeText.Text = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) ? "Admin" : "Standard";
        }
        catch
        {
            AdminBadgeText.Text = "Standard";
        }
    }

    private void UpdatePauseButtonState()
    {
        var paused = LiveReadingsState.IsPaused;
        PauseButtonText.Text = paused ? "Resume" : "Pause";
        PauseButtonIcon.Symbol = paused ? Microsoft.UI.Xaml.Controls.Symbol.Play : Microsoft.UI.Xaml.Controls.Symbol.Pause;
        if (PauseToolTip is ToolTip tt)
            tt.Content = paused ? "Resume live updates" : "Pause live updates (hold Ctrl to pause)";
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        LiveReadingsState.TogglePauseByUser();
    }

    // Export/report functionality has been removed.
}
