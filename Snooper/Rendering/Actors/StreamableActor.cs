using CUE4Parse.UE4.Assets.Exports;

namespace Snooper.Rendering.Actors;

public abstract class StreamableActor : Actor
{
    public bool IsLoaded { get; private set; }
    public bool IsLoading { get; private set; }
    public bool CanLoad => !IsLoaded && !IsLoading && OnLoad != null;

    protected event Action? OnLoad;

    protected StreamableActor(UObject actor) : base(actor)
    {
        IsVisible = false;
    }

    public void Load()
    {
        if (!CanLoad) return;

        IsVisible = true;
        IsLoading = true;
        ActorManager?.ThreadManager.Enqueue(() =>
        {
            try
            {
                OnLoad?.Invoke();
                IsLoaded = true;
            }
            finally
            {
                IsLoading = false;
            }
        });
    }

    public Action? GetLoadAction()
    {
        if (!CanLoad) return null;

        return () =>
        {
            try
            {
                OnLoad?.Invoke();
                IsLoaded = true;
            }
            finally
            {
                IsLoading = false;
            }
        };
    }
}
