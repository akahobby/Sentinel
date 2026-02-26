using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;

namespace Sentinel.App.Pages;

public sealed partial class ServicesPage : Page
{
    public ServicesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var collector = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IServicesCollector)) as Sentinel.Core.Interfaces.IServicesCollector;
        if (collector != null)
        {
            DataContext = new ServicesViewModel(collector);
            _ = (DataContext as ServicesViewModel)?.RefreshCommand.ExecuteAsync(null);
        }
    }
}
