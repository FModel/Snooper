using System.Collections.Concurrent;
using Serilog;

namespace Snooper.Core;

public static class MainThreadQueue
{
    private static readonly ConcurrentQueue<Action> _lazyQueue = [];
    
    public static void AddToLazyQueue(Action action)
    {
        _lazyQueue.Enqueue(action);
    }
    
    public static void Dequeue(int limit = 0)
    {
        var count = 0;
        while (!_lazyQueue.IsEmpty && (limit == 0 || count < limit))
        {
            if (_lazyQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error executing action from MainThreadQueue");
                }
                finally
                {
                    count++;
                }
            }
        }
    }
}