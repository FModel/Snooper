using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public abstract class ActorManager : IGameSystem
{
    private static readonly Dictionary<Type, Func<ActorSystem>> _registeredFactories = [];
    private readonly Dictionary<Type, List<ActorSystem>> _systemsPerComponentType = [];
    private readonly HashSet<Actor> _actors = [];

    public static void RegisterSystemFactory<T>() where T : ActorSystem, new()
    {
        _registeredFactories.Add(typeof(T), () => new T());
    }

    public static void RegisterSystemFactory<T>(Func<T> factory) where T : ActorSystem
    {
        _registeredFactories.Add(typeof(T), factory);
    }

    public SortedList<uint, ActorSystem> Systems { get; } = [];

    public virtual void Load()
    {
        foreach (var system in Systems.Values)
        {
            system.Load();
        }
    }

    public virtual void Update(float delta)
    {
        foreach (var system in Systems.Values)
        {
            system.Update(delta);
        }
    }

    public virtual void Render(CameraComponent camera)
    {
        foreach (var system in Systems.Values)
        {
            system.Render(camera);
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
        if (_actors.Contains(actor)) return;
        if (actor.ActorManager != null)
        {
            throw new ArgumentException("This actor is already used by another actor manager.", nameof(actor));
        }

        actor.ActorManager = this;
        _actors.Add(actor);

        foreach (var component in actor.Components)
        {
            AddComponent(component, actor);
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
        if (!_actors.Remove(actor)) return;

        actor.Components.CollectionChanged -= OnComponentsCollectionChanged;
        actor.Children.CollectionChanged -= OnChildrenCollectionChanged;

        foreach (var component in actor.Components)
        {
            RemoveComponent(component, actor);
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
            foreach (var system in Systems.Values)
            {
                if (system.Accepts(componentType))
                {
                    systemsForComponent.Add(system);
                }
            }
            _systemsPerComponentType.Add(componentType, systemsForComponent);
        }

        foreach (var system in systemsForComponent)
        {
            system.ProcessActorComponent(component, actor);
        }
    }

    private void CollectNewActorSystems(Type componentType)
    {
        var actorSystemAttributes = componentType.GetCustomAttributes<DefaultActorSystemAttribute>();
        foreach (var actorSystemAttribute in actorSystemAttributes)
        {
            var addNewSystem = Systems.All(s => s.GetType() != actorSystemAttribute.Type);
            if (addNewSystem)
            {
                if (_registeredFactories.TryGetValue(actorSystemAttribute.Type, out Func<ActorSystem>? factory))
                {
                    var system = factory();
                    system.ActorManager = this;

                    Systems.Add(system.Order, system);
                    return;
                }

                throw new InvalidOperationException($"{actorSystemAttribute.Type.Name} is not yet registered");
            }
        }
        throw new InvalidOperationException($"trying to add {componentType.Name} but no ActorSystem attribute exists for it");
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

    public void Dispose()
    {
        foreach (var system in Systems.Values)
        {
            system.Dispose();
        }
        Systems.Clear();
    }
}
