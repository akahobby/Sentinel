using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sentinel.App.ViewModels;
using Sentinel.Core.Interfaces;

namespace Sentinel.App.Pages;

public sealed partial class ZeroTracePage : Page
{
    public ZeroTracePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _liveCleanup = true;
        _fullCleanup = true;
        if (e.Parameter is ZeroTraceNavParams p)
        {
            _liveCleanup = p.LiveCleanup;
            _fullCleanup = p.FullCleanup;
        }
    }

    private bool _liveCleanup;
    private bool _fullCleanup = true;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var runner = App.Services?.GetService(typeof(IToolsRunner)) as IToolsRunner;
        if (runner == null) return;
        var vm = new ZeroTraceViewModel(runner)
        {
            LiveCleanup = _liveCleanup,
            FullCleanup = _fullCleanup
        };
        DataContext = vm;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(ZeroTraceViewModel.Step)) return;
            UpdateStepVisibility(vm.Step);
        };
        UpdateStepVisibility(vm.Step);
        _ = vm.LoadAppsCommand.ExecuteAsync(null);
        SearchBox.TextChanged += (_, _) =>
        {
            if (DataContext is ZeroTraceViewModel v)
                v.SearchText = SearchBox.Text ?? "";
        };
    }

    private void UpdateStepVisibility(ZeroTraceViewModel.ZeroTraceStep step)
    {
        AppList.Visibility = step == ZeroTraceViewModel.ZeroTraceStep.AppPicker || step == ZeroTraceViewModel.ZeroTraceStep.Scanning ? Visibility.Visible : Visibility.Collapsed;
        TargetList.Visibility = step == ZeroTraceViewModel.ZeroTraceStep.Audit ? Visibility.Visible : Visibility.Collapsed;
        AuditButtonsPanel.Visibility = step == ZeroTraceViewModel.ZeroTraceStep.Audit ? Visibility.Visible : Visibility.Collapsed;
        ProceedButton.Visibility = step == ZeroTraceViewModel.ZeroTraceStep.Audit && (DataContext as ZeroTraceViewModel)?.LiveCleanup == true ? Visibility.Visible : Visibility.Collapsed;
        DoneScanOnlyButton.Visibility = step == ZeroTraceViewModel.ZeroTraceStep.Audit && (DataContext as ZeroTraceViewModel)?.LiveCleanup != true ? Visibility.Visible : Visibility.Collapsed;
    }
}
