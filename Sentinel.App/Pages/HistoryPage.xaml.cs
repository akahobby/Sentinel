using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;

namespace Sentinel.App.Pages;

public sealed partial class HistoryPage : Page
{
    public HistoryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var storage = App.Services?.GetService(typeof(Sentinel.Core.Interfaces.IStorageService)) as Sentinel.Core.Interfaces.IStorageService;
        if (storage != null)
        {
            DataContext = new HistoryViewModel(storage);
            _ = (DataContext as HistoryViewModel)?.LoadCommand.ExecuteAsync(null);
        }
    }
}
