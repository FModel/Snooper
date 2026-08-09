using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Managers;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Snooper.Core.Systems;

public enum ActorSystemType
{
    Rendering,
    Physics,
    Animation,
    Input,
    Audio,
    Custom
}

public abstract class ActorSystem : IGameSystem
{
    public readonly string DisplayName;
    public readonly Type ComponentType;

    public bool IsEnabled = true;
    public bool ShowWireframe = false;
    public ActorManager? ActorManager { get; internal set; }

    public abstract ActorSystemType SystemType { get; }
    public abstract uint Order { get; }
    public abstract int Capacity { get; }
    public abstract int ComponentsCount { get; }
    public abstract int EnqueuedComponentsCount { get; }
    public abstract int DirtyComponentsCount { get; }
    public abstract uint? MaxBindingUsed { get; }

    protected ActorSystem(Type componentType)
    {
        DisplayName = GetType().Name;
        ComponentType = componentType;
    }

    public void Load()
    {
        if (!IsEnabled) return;
        OnLoad();
    }

    public void Update(float delta)
    {
        if (!IsEnabled) return;
        using (Profiler.Cpu(DisplayName))
        {
            OnUpdate(delta);
        }
    }

    protected abstract void OnLoad();
    protected abstract void OnUpdate(float delta);

    public abstract void RegisterComponent(ActorComponent component, Actor actor);
    public abstract void UnregisterComponent(ActorComponent component, Actor actor, EEndPlayReason reason);

    protected virtual bool AllowDerivation => true;
    public virtual bool Accepts(Type type)
    {
        if (!AllowDerivation) return ComponentType == type;
        return ComponentType.IsAssignableFrom(type);
    }

    public virtual void Dispose()
    {
        ActorManager = null;
    }
}

public abstract class ActorSystem<TComponent>() : ActorSystem(typeof(TComponent)) where TComponent : ActorComponent
{
    public override int Capacity => -1; // unlimited
    public override int ComponentsCount => Components.Count;
    public override int EnqueuedComponentsCount => _componentsToLoad.Count;
    public override int DirtyComponentsCount => DirtyComponents.Count;
    public override uint? MaxBindingUsed => null;

    protected bool ClearMaskBuffer { get; private set; } = false;

    protected HashSet<TComponent> Components { get; } = [];
    protected HashSet<TComponent> DirtyComponents { get; } = [];

    protected override void OnLoad()
    {
        if (MaxBindingUsed is not { } max) return;

        var limit = ActorManager?.Renderer.DeviceInfo.MaxShaderStorageBufferBindings;
        if (max > limit)
        {
            // TODO: should we actually limit or let it crash?
            Log.Warning("{SystemName} uses {MaxBindingUsed} shader storage buffer bindings, which exceeds the device limit of {Limit}. This may cause rendering issues.", DisplayName, max, limit);
        }
    }

    protected override void OnUpdate(float delta)
    {
        DequeueComponents(5);
        if (DirtyComponents.Count == 0) return;

        var components = DirtyComponents.ToArray();
        DirtyComponents.Clear();

        PreOnUpdate(components);
        foreach (var component in components)
        {
            OnComponentUpdate(component, delta);
        }
        PostOnUpdate();
    }

    public sealed override void RegisterComponent(ActorComponent component, Actor actor)
    {
        if (component is not TComponent actorComponent)
            throw new ArgumentException("The actor component must be assignable to TComponent", nameof(component));

        if (Components.Contains(actorComponent) || !CanEnqueueActorComponent(actorComponent)) return;

        _componentsToLoad.Enqueue(actorComponent);
        OnActorComponentEnqueued(actorComponent);
    }

    public sealed override void UnregisterComponent(ActorComponent component, Actor actor, EEndPlayReason reason)
    {
        if (component is not TComponent actorComponent)
            throw new ArgumentException("The actor component must be assignable to TComponent", nameof(component));

        if (!Components.Remove(actorComponent)) return;

        Log.Debug("Removing component {ComponentName} from actor {ActorName} in system {SystemName}.", actorComponent.Name, actor.Name, DisplayName);
        OnActorComponentRemoved(actorComponent, reason);
    }

    protected virtual bool CanEnqueueActorComponent(TComponent component)
    {
        return Capacity < 0 || Capacity > (EnqueuedComponentsCount + ComponentsCount);
    }

    public IEnumerable<T> GetComponents<T>() where T : TComponent => Components.OfType<T>();

    protected virtual void OnActorComponentEnqueued(TComponent component)
    {

    }

    protected virtual void OnActorComponentAdded(TComponent component)
    {
        component.OnRequestSystemUpdate += OnComponentRequestUpdate;
        DirtyComponents.Add(component);
    }

    protected virtual void OnActorComponentRemoved(TComponent component, EEndPlayReason reason)
    {
        component.OnRequestSystemUpdate -= OnComponentRequestUpdate;
        DirtyComponents.Remove(component);
    }

    protected virtual void PreOnUpdate(TComponent[] components)
    {

    }

    protected virtual void OnComponentUpdate(TComponent component, float delta)
    {

    }

    protected virtual void PostOnUpdate()
    {
        ClearMaskBuffer = false;
    }

    private void OnComponentRequestUpdate(ActorComponent component)
    {
        if (component is not TComponent actorComponent)
            throw new ArgumentException("The actor component must be assignable to TComponent", nameof(component));

        if (Components.Contains(actorComponent))
        {
            DirtyComponents.Add(actorComponent);

            if (actorComponent.IsDirty(DirtyFlags.Outline))
            {
                ClearMaskBuffer = true;
            }
        }
    }

    private readonly Queue<TComponent> _componentsToLoad = [];
    private void DequeueComponents(int limit = 0)
    {
        var count = 0;
        while (_componentsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var component = _componentsToLoad.Dequeue();
            if (component.Scene != ActorManager) continue; // it may have ended play while it waited its turn

            if (Components.Add(component))
            {
                OnActorComponentAdded(component);
            }
            count++;
        }
    }
}
