using System.Windows.Input;

namespace Sentinel.App.Infrastructure;

public interface IAsyncCommand : ICommand
{
    Task ExecuteAsync(object? parameter);
}
