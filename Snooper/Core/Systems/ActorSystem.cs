using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Managers;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
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
    public readonly Type ComponentType;
    public readonly SystemProfiler Profiler;

    public bool IsEnabled = true;
    public ActorManager? ActorManager { get; internal set; }
    public float Time { get; private set; }

    public abstract ActorSystemType SystemType { get; }
    public abstract uint Order { get; }
    public abstract int ComponentsCount { get; }
    public abstract int EnqueuedComponentsCount { get; }

    protected ActorSystem(Type componentType)
    {
        DisplayName = GetType().Name;
        ComponentType = componentType;
        Profiler = new SystemProfiler();
    }

    public void Load()
    {
        if (!IsEnabled) return;
        Profiler.Time(ProfilerMetric.Load, OnLoad);
    }

    public void Update(float delta)
    {
        if (!IsEnabled) return;
        Profiler.Time(ProfilerMetric.Update, () =>
        {
            Time += delta;
            OnUpdate(delta);
        });
    }

    public void Render(CameraComponent camera)
    {
        if (!IsEnabled) return;
        Profiler.Time(ProfilerMetric.CpuRender, () =>
        {
            Profiler.BeginQuery(QueryTarget.TimeElapsed, QueryTarget.PrimitivesGenerated);
            OnRender(camera);
            Profiler.EndQuery();
        });
    }

    protected abstract void OnLoad();
    protected abstract void OnUpdate(float delta);
    protected abstract void OnRender(CameraComponent camera);

    public abstract void ProcessActorComponent(ActorComponent component, Actor actor);

    protected virtual bool AllowDerivation => true;
    public virtual bool Accepts(Type type)
    {
        if (!AllowDerivation) return ComponentType == type;
        return ComponentType.IsAssignableFrom(type);
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

    protected override void OnLoad() => DequeueComponents();
    protected override void OnUpdate(float delta) => DequeueComponents(5);

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
                Log.Debug("Removing component {ComponentName} from actor {ActorName} in system {SystemName}.", actorComponent.Name, actor.Name, DisplayName);
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
