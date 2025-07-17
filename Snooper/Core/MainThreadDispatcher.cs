using System.Collections.Concurrent;
using Serilog;

namespace Snooper.Core;

public static class MainThreadDispatcher
{
    private static readonly ConcurrentQueue<Action> _lazyQueue = [];
    
    public static bool IsEmpty => _lazyQueue.IsEmpty;
    
    public static void Enqueue(Action action)
    {
        _lazyQueue.Enqueue(action);
    }
    
    public static void Dequeue(int limit = 0)
    {
        var count = 0;
        while (!IsEmpty && (limit == 0 || count < limit))
        {
            if (_lazyQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error executing action from MainThreadDispatcher");
                }
                finally
                {
                    count++;
                }
            }
        }
    }
    
    public static Task RunAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}