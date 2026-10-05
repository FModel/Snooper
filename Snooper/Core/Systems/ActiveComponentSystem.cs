using Snooper.Core.Containers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Core.Systems;

public interface IActiveComponentSystem : IGameSystem
{
    public bool Accepts(Type type);
    public ActorComponent? Current { get; }
    public bool IsCurrent(ActorComponent component);
    public void Select(ActorComponent component);
}

/// <summary>
/// for a component a scene only uses one of at a time
/// the system knows which one is in effect
/// </summary>
public abstract class ActiveComponentSystem<TComponent> : ActorSystem<TComponent>, IActiveComponentSystem, IMemoryDetailsProvider where TComponent : SpatialComponent
{
    private readonly List<TComponent> _components = [];

    public TComponent? Selected { get; private set; }

    // the selected one, else the only visible one added after the first (ours), else the first (ours)
    public TComponent? Current
    {
        get
        {
            if (Selected != null || _components.Count == 0) return Selected;

            TComponent? other = null;
            for (var i = 1; i < _components.Count; i++)
            {
                if (_components[i] is not { IsVisible: true, Actor.IsVisibleRecursive: true } component || !CanTakeOver(component)) continue;
                if (other != null) return _components[0];

                other = component;
            }

            return other ?? _components[0];
        }
    }

    ActorComponent? IActiveComponentSystem.Current => Current;
    public TComponent? Active => IsEnabled && Current is { IsVisible: true, Actor.IsVisibleRecursive: true } component ? component : null;

    public bool IsCurrent(ActorComponent component)
    {
        return Current == component;
    }

    public void Select(ActorComponent component)
    {
        Selected = component as TComponent;
    }

    protected virtual bool CanTakeOver(TComponent component)
    {
        return true;
    }

    protected override void OnActorComponentAdded(TComponent component)
    {
        base.OnActorComponentAdded(component);

        _components.Add(component);
    }

    protected override void OnActorComponentRemoved(TComponent component, EEndPlayReason reason)
    {
        base.OnActorComponentRemoved(component, reason);

        _components.Remove(component);
        if (component == Selected) Selected = null;
    }

    public virtual long Allocated => 0;
    public virtual long Used => 0;

    public virtual IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        return [];
    }
}
