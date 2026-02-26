using System.Collections.Concurrent;

namespace Sentinel.App.Infrastructure;

public static class Debouncer
{
    private static readonly ConcurrentDictionary<object, CancellationTokenSource> _tokens = new();

    public static void Debounce(object key, TimeSpan delay, Action action)
    {
        if (_tokens.TryRemove(key, out var cts))
            cts.Cancel();
        cts = new CancellationTokenSource();
        _tokens[key] = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
            }
            catch (TaskCanceledException) { return; }
            if (!cts.IsCancellationRequested && _tokens.TryRemove(key, out _))
                action();
        });
    }

    public static async Task DebounceAsync(object key, TimeSpan delay, Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (_tokens.TryRemove(key, out var cts))
            cts.Cancel();
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _tokens[key] = cts;
        try
        {
            await Task.Delay(delay, cts.Token);
        }
        catch (TaskCanceledException) { return; }
        if (!cts.IsCancellationRequested && _tokens.TryRemove(key, out _))
            await action();
    }
}
