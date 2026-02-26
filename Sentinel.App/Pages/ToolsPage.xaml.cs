using Microsoft.UI.Xaml.Controls;
using Sentinel.App.ViewModels;
using Sentinel.Core.Interfaces;

namespace Sentinel.App.Pages;

public sealed partial class ToolsPage : Page
{
    public ToolsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var runner = App.Services?.GetService(typeof(IToolsRunner)) as IToolsRunner;
        if (runner != null)
            DataContext = new ToolsViewModel(runner);
    }
}
