using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Managers;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

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
    public readonly SystemProfiler Profiler;

    public bool IsEnabled = true;
    public bool ShowWireframe = false;
    public ActorManager? ActorManager { get; internal set; }
    public float Time { get; private set; }

    public abstract ActorSystemType SystemType { get; }
    public abstract uint Order { get; }
    public abstract int Capacity { get; }
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

    protected abstract void OnLoad();
    protected abstract void OnUpdate(float delta);

    public abstract void ProcessActorComponent(ActorComponent component, Actor actor);

    protected virtual bool AllowDerivation => true;
    public virtual bool Accepts(Type type)
    {
        if (!AllowDerivation) return ComponentType == type;
        return ComponentType.IsAssignableFrom(type);
    }

    public virtual void Dispose()
    {
        Profiler.Dispose();
        ActorManager = null;
    }
}

public abstract class ActorSystem<TComponent>() : ActorSystem(typeof(TComponent)), IRenderSystem where TComponent : ActorComponent
{
    public override int Capacity => -1; // unlimited
    public override int ComponentsCount => Components.Count;
    public override int EnqueuedComponentsCount => _componentsToLoad.Count;

    protected DebugVisualizationMode DebugColorMode => ActorManager?.DebugColorMode ?? DebugVisualizationMode.None;
    protected bool ClearMaskBuffer { get; private set; } = false;

    protected HashSet<TComponent> Components { get; } = [];
    protected HashSet<TComponent> DirtyComponents { get; } = [];

    protected override void OnLoad() => DequeueComponents();
    protected override void OnUpdate(float delta)
    {
        DequeueComponents(5);
        if (DirtyComponents.Count == 0) return;

        var components = DirtyComponents.ToArray();
        DirtyComponents.Clear();

        PreOnUpdate();
        foreach (var component in components)
        {
            OnComponentUpdate(component, delta);
        }
        PostOnUpdate();
    }

    public void Render(CameraComponent camera, CommandBufferType type)
    {
        if (!IsEnabled) return;
        Profiler.Time(ProfilerMetric.CpuRender, () =>
        {
            Profiler.BeginQuery(QueryTarget.TimeElapsed, QueryTarget.PrimitivesGenerated);

            if (ShowWireframe) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            OnRender(camera, type);
            if (ShowWireframe) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

            Profiler.EndQuery();
        });
    }
    protected abstract void OnRender(CameraComponent camera, CommandBufferType type);

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
        return Capacity < 0 || Capacity > (EnqueuedComponentsCount + ComponentsCount); // TODO: some systems override this without calling base
    }

    protected virtual void OnActorComponentEnqueued(TComponent component)
    {

    }

    protected virtual void OnActorComponentAdded(TComponent component)
    {
        component.OnRequestSystemUpdate += OnComponentRequestUpdate;
        component.MarkDirty(DirtyFlags.All);
    }

    protected virtual void OnActorComponentRemoved(TComponent component)
    {
        component.OnRequestSystemUpdate -= OnComponentRequestUpdate;
        DirtyComponents.Remove(component);
    }

    protected virtual void PreOnUpdate()
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

            if (actorComponent.IsDirty(DirtyFlags.Selection))
            {
                ClearMaskBuffer = true;
            }
        }
    }

    private readonly Queue<TComponent?> _componentsToLoad = [];
    private void DequeueComponents(int limit = 0)
    {
        var count = 0;
        while (_componentsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var component = _componentsToLoad.Dequeue();
            if (component == null) continue; // TODO: sometimes components just disappear from the queue

            if (Components.Add(component))
            {
                OnActorComponentAdded(component);
            }
            count++;
        }
    }
}
