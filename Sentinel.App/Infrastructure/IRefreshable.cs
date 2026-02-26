namespace Sentinel.App.Infrastructure;

public interface IRefreshable
{
    Task RefreshAsync();
}
