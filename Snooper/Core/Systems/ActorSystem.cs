using Serilog;
using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public enum ActorSystemType
{
    Deferred,
    Forward,
    Physics,
    Animation,
    Input,
    Audio,
    Custom
}

public abstract class ActorSystem : IGameSystem
{
    public readonly string DisplayName;
    public readonly Type? ComponentType;
    public readonly SystemProfiler Profiler;
    public ActorManager? ActorManager { get; internal set; }
    public float Time { get; private set; }
    
    public abstract ActorSystemType SystemType { get; }
    public abstract uint Order { get; }
    public abstract int ComponentsCount { get; }
    public abstract int EnqueuedComponentsCount { get; }

    protected ActorSystem(Type? componentType)
    {
        DisplayName = GetType().Name;
        ComponentType = componentType;
        Profiler = new SystemProfiler();
    }

    public abstract void Load();
    public virtual void Update(float delta) => Time += delta;
    public abstract void Render(CameraComponent camera);
    
    public abstract void ProcessActorComponent(ActorComponent component, Actor actor);
    
    protected virtual bool AllowDerivation => true;
    public bool Accepts(Type type)
    {
        if (!AllowDerivation) return ComponentType == type;
        return ComponentType?.IsAssignableFrom(type) ?? false;
    }

    protected ActorDebugColorMode DebugColorMode => ActorManager?.DebugColorMode ?? ActorDebugColorMode.None;

    public virtual void Dispose()
    {
        Profiler.Dispose();
        ActorManager = null;
    }
}

public abstract class ActorSystem<TComponent>() : ActorSystem(typeof(TComponent)) where TComponent : ActorComponent
{
    public override ActorSystemType SystemType => ActorSystemType.Forward;
    public override int ComponentsCount => Components.Count;
    public override int EnqueuedComponentsCount => _componentsToLoad.Count;
    
    protected HashSet<TComponent> Components { get; } = [];

    public override void Load() => DequeueComponents();
    public override void Update(float delta)
    {
        base.Update(delta);
        DequeueComponents(5);
    }

    public sealed override void ProcessActorComponent(ActorComponent component, Actor actor)
    {
        if (component is not TComponent actorComponent)
            throw new ArgumentException("The actor component must be assignable to TComponent", nameof(component));
        
        switch (Components.Contains(actorComponent))
        {
            case false when CanEnqueueActorComponent(actorComponent):
                _componentsToLoad.Enqueue(actorComponent);
                OnActorComponentEnqueued(actorComponent);
                break;
            case true:
                Log.Debug("Removing component {ComponentName} from actor {ActorName} in system {SystemName}.", actorComponent.DisplayName, actor.Name, DisplayName);
                Components.Remove(actorComponent);
                OnActorComponentRemoved(actorComponent);
                break;
        }
    }
    
    protected virtual bool CanEnqueueActorComponent(TComponent component)
    {
        return true;
    }
    
    protected virtual void OnActorComponentEnqueued(TComponent component)
    {

    }

    protected virtual void OnActorComponentAdded(TComponent component)
    {

    }

    protected virtual void OnActorComponentRemoved(TComponent component)
    {

    }

    private readonly Queue<TComponent> _componentsToLoad = [];
    private void DequeueComponents(int limit = 0)
    {
        var count = 0;
        while (_componentsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var component = _componentsToLoad.Dequeue();
            if (Components.Add(component))
            {
                OnActorComponentAdded(component);
            }
            count++;
        }
    }
}
