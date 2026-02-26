using Microsoft.UI.Xaml.Controls;

namespace Sentinel.App.Infrastructure;

public sealed class NavigationService : INavigationService
{
    private Frame? _frame;
    private readonly Dictionary<string, Type> _pages = new();

    public string CurrentPageKey { get; private set; } = "Overview";

    public event EventHandler<string>? Navigated;

    public void SetFrame(Frame frame) => _frame = frame;

    public void Register(string key, Type pageType) => _pages[key] = pageType;

    public void NavigateTo(string pageKey, object? parameter = null)
    {
        if (_frame == null || !_pages.TryGetValue(pageKey, out var pageType)) return;
        _frame.Navigate(pageType, parameter);
        CurrentPageKey = pageKey;
        Navigated?.Invoke(this, pageKey);
    }
}
