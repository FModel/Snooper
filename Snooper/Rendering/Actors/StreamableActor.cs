using System.Numerics;
using CUE4Parse.UE4.Assets.Exports;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Actors;

public enum EStreamingState
{
    Unloaded,
    Loading,
    Loaded,
    Failed
}

public abstract class StreamableActor(UObject actor, bool isPersistent = false) : Actor(actor)
{
    public readonly bool IsPersistent = isPersistent;
    public bool Is2D { get; init; }

    public EStreamingState State { get; internal set; }
    public bool IsLoaded => State is EStreamingState.Loaded;
    public bool IsLoading => State is EStreamingState.Loading;
    public bool CanLoad => State is EStreamingState.Unloaded or EStreamingState.Failed && CanBuild;

    internal bool Wanted { get; private set; }

    public void Load()
    {
        if (!CanBuild) return;

        Wanted = true;
        ActorManager?.Streaming.Load(this);
    }

    public void Unload()
    {
        Wanted = false;
        ActorManager?.Streaming.Unload(this);
    }

    public float DistanceTo(Vector3 position)
    {
        if (LoadingBounds is not { } bounds) return float.PositiveInfinity;

        var closest = Vector3.Clamp(position, bounds.Center - bounds.Extents, bounds.Center + bounds.Extents);
        if (Is2D) closest.Y = position.Y;

        return Vector3.Distance(position, closest);
    }

    protected abstract CullingBounds? LoadingBounds { get; }
    protected abstract bool CanBuild { get; }
    protected internal abstract Actor Build();
}
