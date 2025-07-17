using System.Collections.Specialized;
using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Hardware;
using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public abstract class ActorManager : IGameSystem
{
    private static Func<ActorSystem, bool> IsSystemNotOfType(Type type) => x => x.GetType() != type;
    
    private static readonly Dictionary<Type, Func<ActorSystem>> _registeredFactories = [];
    private readonly Dictionary<Type, List<ActorSystem>> _systemsPerComponentType = [];
    // private readonly HashSet<FGuid> _actors = [];

    public bool DebugMode = false;
    public bool DrawBoundingBoxes = false;
    public ActorDebugColorMode DebugColorMode = ActorDebugColorMode.None;

    public static void RegisterSystemFactory<T>() where T : ActorSystem, new() => RegisterSystemFactory(() => new T());
    public static void RegisterSystemFactory<T>(Func<T> factory) where T : ActorSystem
    {
        _registeredFactories.Add(typeof(T), factory);
    }

    public ContextInfo Context { get; private set; }
    public Dictionary<string, Texture> Icons { get; } = new();
    public SortedList<uint, ActorSystem> Systems { get; } = [];

    public virtual void Load()
    {
        Context = new ContextInfo();
        
        var icons = new[] {"bone.png", "cube.png", "mountain.png", "sphere.png", "sun.png", "video.png"};
        foreach (var icon in icons)
        {
            var texture = new EmbeddedTexture2D(icon);
            texture.Generate();
                
            Icons.Add(icon[..^4], texture);
        }
        
        DequeueSystems();
    }

    public virtual void Update(float delta)
    {
        DequeueSystems(1);
        foreach (var system in Systems.Values)
        {
            system.Update(delta);
        }
        
        MainThreadDispatcher.Dequeue(1);
    }

    [Obsolete("Use Render(CameraComponent camera, ActorSystemType systemType) instead.")]
    public void Render(CameraComponent camera) => Render(camera, ActorSystemType.Forward);
    protected void Render(CameraComponent camera, ActorSystemType systemType)
    { 
        var queries = new[] {QueryTarget.TimeElapsed, QueryTarget.PrimitivesGenerated};
        
        foreach (var system in Systems.Values.Where(x => x.SystemType == systemType))
        {
            system.Profiler.BeginQuery(queries);
            system.Render(camera);
            system.Profiler.EndQuery();
        }
    }

    protected void AddRoot(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new ArgumentException("This actor should not have a parent.", nameof(actor));
        }

        AddInternal(actor);
    }

    private void AddInternal(Actor actor)
    {
        // if (_actors.Contains(actor.Guid))
        //     throw new ArgumentException("This actor is already added to the actor manager.", nameof(actor));
        if (actor.ActorManager != null)
            throw new ArgumentException("This actor is already used by another actor manager.", nameof(actor));

        actor.ActorManager = this;
        // _actors.Add(actor.Guid);

        for (var i = 0; i < actor.Components.Count; i++)
        {
            AddComponent(actor.Components[i], actor);
        }

        foreach (var child in actor.Children)
        {
            AddInternal(child);
        }

        actor.Children.CollectionChanged += OnChildrenCollectionChanged;
        actor.Components.CollectionChanged += OnComponentsCollectionChanged;
    }

    protected void RemoveRoot(Actor actor)
    {
        RemoveInternal(actor);
    }

    private void RemoveInternal(Actor actor)
    {
        // if (!_actors.Remove(actor.Guid)) return;

        actor.Components.CollectionChanged -= OnComponentsCollectionChanged;
        actor.Children.CollectionChanged -= OnChildrenCollectionChanged;

        for (var i = 0; i < actor.Components.Count; i++)
        {
            RemoveComponent(actor.Components[i], actor);
        }

        foreach (var child in actor.Children)
        {
            RemoveInternal(child);
        }

        actor.ActorManager = null;
    }

    protected virtual void AddComponent(ActorComponent component, Actor actor)
    {
        CheckActorComponentWithSystems(component, actor, false);
    }

    protected virtual void RemoveComponent(ActorComponent component, Actor actor)
    {
        CheckActorComponentWithSystems(component, actor, true);
    }

    private void CheckActorComponentWithSystems(ActorComponent component, Actor actor, bool bRemove)
    {
        var componentType = component.GetType();
        if (!_systemsPerComponentType.TryGetValue(componentType, out var systemsForComponent))
        {
            if (!bRemove)
            {
                CollectNewActorSystems(componentType);
            }

            systemsForComponent = [];
            foreach (var system in _systemsToLoad) AddIfAccepted(system);
            foreach (var system in Systems.Values) AddIfAccepted(system);
            _systemsPerComponentType.Add(componentType, systemsForComponent);
        }

        foreach (var system in systemsForComponent)
        {
            system.ProcessActorComponent(component, actor);
        }

        void AddIfAccepted(ActorSystem system)
        {
            if (system.Accepts(componentType))
            {
                systemsForComponent.Add(system);
            }
        }
    }

    private void CollectNewActorSystems(Type componentType)
    {
        var actorSystemAttributes = componentType.GetCustomAttributes<DefaultActorSystemAttribute>();
        foreach (var actorSystemAttribute in actorSystemAttributes)
        {
            var addNewSystem = _systemsToLoad.All(IsSystemNotOfType(actorSystemAttribute.Type)) && Systems.Values.All(IsSystemNotOfType(actorSystemAttribute.Type));
            if (addNewSystem)
            {
                if (_registeredFactories.TryGetValue(actorSystemAttribute.Type, out Func<ActorSystem>? factory))
                {
                    var system = factory();
                    system.ActorManager = this;

                    _systemsToLoad.Enqueue(system);
                    return;
                }

                throw new InvalidOperationException($"{actorSystemAttribute.Type.Name} is not yet registered");
            }
        }
    }

    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var actor in e.NewItems!.Cast<Actor>())
                {
                    AddInternal(actor);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var actor in e.OldItems!.Cast<Actor>())
                {
                    RemoveInternal(actor);
                }
                break;
        }
    }

    private void OnComponentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not ActorComponentCollection componentCollection) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var component in e.NewItems!.Cast<ActorComponent>())
                {
                    AddComponent(component, componentCollection.Actor);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var component in e.OldItems!.Cast<ActorComponent>())
                {
                    RemoveComponent(component, componentCollection.Actor);
                }
                break;
        }
    }
    
    private readonly Queue<ActorSystem> _systemsToLoad = [];
    private void DequeueSystems(int limit = 0)
    {
        var count = 0;
        while (_systemsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var system = _systemsToLoad.Dequeue();
            system.Load();
            
            Systems.Add(system.Order, system);
            count++;
        }
    }

    public void Dispose()
    {
        foreach (var system in Systems.Values)
        {
            system.Dispose();
        }
        Systems.Clear();
    }
}
