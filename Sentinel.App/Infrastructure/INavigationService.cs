using Microsoft.UI.Xaml.Controls;

namespace Sentinel.App.Infrastructure;

public interface INavigationService
{
    void SetFrame(Frame frame);
    void Register(string key, Type pageType);
    void NavigateTo(string pageKey, object? parameter = null);
    string CurrentPageKey { get; }
    event EventHandler<string>? Navigated;
}
