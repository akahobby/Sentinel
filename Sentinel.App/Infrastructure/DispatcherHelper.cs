using Microsoft.UI.Dispatching;

namespace Sentinel.App.Infrastructure;

public static class DispatcherHelper
{
    public static DispatcherQueue? AppDispatcher { get; set; }

    public static void RunOnUiThread(Action action)
    {
        if (AppDispatcher == null) return;
        if (AppDispatcher.HasThreadAccess)
            action();
        else
            AppDispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }

    public static async Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (AppDispatcher == null) return;
        if (AppDispatcher.HasThreadAccess)
            await action();
        else
        {
            var tcs = new TaskCompletionSource<bool>();
            AppDispatcher.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult(true);
                }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            await tcs.Task;
        }
    }
}
