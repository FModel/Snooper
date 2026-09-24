using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Snooper.Rendering.Actors;

namespace Snooper.Core.Managers;

internal sealed class StreamingQueue
{
    private static readonly ILogger Log = Serilog.Log.ForContext(Constants.SourceContextPropertyName, nameof(StreamingQueue));

    private const int MaxConcurrentBuilds = 2;

    private readonly Queue<StreamableActor> _waiting = new();
    private readonly Queue<StreamableActor> _unloading = new();
    private readonly ConcurrentQueue<(StreamableActor Owner, Actor? Built, Exception? Error)> _built = new();
    private int _building;

    internal void Load(StreamableActor streamable)
    {
        if (streamable.State is not (EStreamingState.Unloaded or EStreamingState.Failed)) return;

        streamable.State = EStreamingState.Loading;
        _waiting.Enqueue(streamable);
    }

    internal void Unload(StreamableActor streamable)
    {
        if (streamable.IsLoaded)
            _unloading.Enqueue(streamable);
    }

    internal void Update(FrameBudget budget)
    {
        while (_building < MaxConcurrentBuilds && _waiting.TryDequeue(out var waiting))
        {
            if (IsWorthIt(waiting)) Build(waiting);
            else waiting.State = EStreamingState.Unloaded;
        }

        while (_built.TryDequeue(out var result))
        {
            _building--;
            Land(result.Owner, result.Built, result.Error);
        }

        var destroyed = 0;
        while (_unloading.Count > 0 && (destroyed == 0 || !budget.Exhausted))
        {
            var unloading = _unloading.Dequeue();
            if (unloading.Wanted || !unloading.IsLoaded) continue; // asked for again in the meantime

            unloading.Children.Clear();
            unloading.State = EStreamingState.Unloaded;
            destroyed++;
        }
    }

    private bool IsWorthIt(StreamableActor streamable) => streamable.Wanted && streamable.ActorManager?.Streaming == this; // still asked for, still in this scene

    private void Build(StreamableActor streamable)
    {
        _building++;
        ThreadManager.Enqueue(() =>
        {
            Actor? built = null;
            Exception? error = null;
            try
            {
                built = streamable.Build();
            }
            catch (Exception e)
            {
                error = e;
            }

            _built.Enqueue((streamable, built, error));
        });
    }

    private void Land(StreamableActor streamable, Actor? built, Exception? error)
    {
        if (error is not null || built is null)
        {
            Log.Error(error, "{Actor} could not be built", streamable.Name);
            streamable.State = EStreamingState.Failed;
            return;
        }

        if (!IsWorthIt(streamable))
        {
            Log.Debug("{Actor} is not wanted anymore, dropping the build", streamable.Name);
            streamable.State = EStreamingState.Unloaded;
            return;
        }

        streamable.Children.Add(built);
        streamable.State = EStreamingState.Loaded;
    }

    internal void Clear()
    {
        while (_built.TryDequeue(out _))
            _building--; // the builds still running come back later and count themselves out

        _waiting.Clear();
        _unloading.Clear();
    }
}
