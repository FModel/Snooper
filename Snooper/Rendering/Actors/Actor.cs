using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class Actor
{
    public string Name { get; }

    public Actor(string name)
    {
        Name = name;

        Components = new ActorComponentCollection(this);
        Children = [];

        Components.CollectionChanged += OnComponentsCollectionChanged;
        Children.CollectionChanged += OnChildrenCollectionChanged;
    }

    public ActorComponentCollection Components { get; }
    public ObservableCollection<Actor> Children { get; }

    private Actor? _parent;
    public Actor? Parent
    {
        get => _parent;
        set
        {
            var old = _parent;
            if (old == value) return;

            old?.Children.Remove(this);
            value?.Children.Add(this);
        }
    }

    public ActorManager? ActorManager { get; internal set; }
    public ActorComponent? RootComponent { get; private set; }
    
    internal readonly int Id = Random.Shared.Next();
    internal virtual string Icon => "cube";

    private void AddInternal(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new InvalidOperationException("This actor already has a parent.");
        }
        
        actor._parent = this;
        
        if (actor.RootComponent is SpatialComponent spatial && RootComponent is SpatialComponent parentSpatial)
        {
            spatial.Relation = parentSpatial;
        }
    }

    private void RemoveInternal(Actor actor)
    {
        if (actor.Parent != this)
        {
            throw new InvalidOperationException("This actor is not a child of the expected parent.");
        }

        actor._parent = null;
    }

    private void AddInternal(ActorComponent component)
    {
        if (component.Actor != null)
        {
            throw new InvalidOperationException("An actor component cannot be set on more than one actor.");
        }
        
        RootComponent ??= component;

        component.Actor = this;
    }

    private void RemoveInternal(ActorComponent component)
    {
        if (component.Actor != this)
        {
            throw new InvalidOperationException("This actor component is not set on this actor.");
        }
        
        if (RootComponent == component)
        {
            RootComponent = null;
        }

        component.Actor = null;
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
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var component in e.NewItems!.Cast<ActorComponent>())
                {
                    AddInternal(component);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var component in e.OldItems!.Cast<ActorComponent>())
                {
                    RemoveInternal(component);
                }
                break;
        }
    }
}
