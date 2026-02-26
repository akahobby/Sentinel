using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;

namespace Sentinel.App.Pages;

public sealed partial class StartupPage : Page
{
    public StartupPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var collector = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IStartupCollector)) as Sentinel.Core.Interfaces.IStartupCollector;
            if (collector != null)
            {
                DataContext = new StartupViewModel(collector);
                _ = (DataContext as StartupViewModel)?.RefreshCommand.ExecuteAsync(null);
            }
        }
        catch (Exception)
        {
            DataContext = new StartupViewModel(null);
            ((StartupViewModel)DataContext).StatusText = "Could not load startup items.";
        }
    }

    private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.ToggleSwitch sw && sw.DataContext is Sentinel.Core.Models.StartupItem item && DataContext is StartupViewModel vm)
            await vm.ToggleEnabledCommand.ExecuteAsync(item);
    }
}
