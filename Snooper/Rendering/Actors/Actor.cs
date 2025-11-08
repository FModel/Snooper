using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Skybox;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class Actor
{
    public string Name { get; }
    public string? ExportType { get; }
    public string? InternalType { get; }
    public bool IsSelected { get; private set; }

    public Actor(string name)
    {
        Name = name;

        Components = new ActorComponentCollection(this);
        Children = [];

        Components.CollectionChanged += OnComponentsCollectionChanged;
        Children.CollectionChanged += OnChildrenCollectionChanged;
    }

    protected Actor(UObject actor) : this(actor.Name)
    {
        if (actor is AActor a && !string.IsNullOrEmpty(a.ActorLabel))
        {
            Name = a.ActorLabel;
        }
        
        ExportType = actor.ExportType;
        InternalType = actor.GetType().Name;
    }

    public ActorComponentCollection Components { get; }
    public ObservableCollection<Actor> Children { get; }

    private Actor? _parent;
    public Actor? Parent
    {
        get => _parent;
        private set
        {
            if (this == value || _parent == value) return;
            
            _parent?.Children.Remove(this);
            value?.Children.Add(this);
        }
    }

    public ActorManager? ActorManager { get; internal set; }
    
    private SpatialComponent? _rootComponent;
    public SpatialComponent? RootComponent
    {
        get => _rootComponent;
        private set
        {
            if (_rootComponent == value) return;
            
            _rootComponent = value;
            
            Icon = _rootComponent?.Icon ?? "component";
        }
    }

    internal readonly int Id = Random.Shared.Next();
    internal string Icon { get; private set; } = "component";

    private void AddInternal(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new InvalidOperationException("This actor already has a parent.");
        }
        
        actor._parent = this;
        
        if (actor.RootComponent != null)
        {
            actor.RootComponent.Relation = RootComponent;
        }
    }

    private void RemoveInternal(Actor actor)
    {
        if (actor.Parent != this)
        {
            throw new InvalidOperationException("This actor is not a child of the expected parent.");
        }

        actor._parent = null;
        
        if (actor.RootComponent != null)
        {
            actor.RootComponent.Relation = null;
        }
    }

    private void AddInternal(ActorComponent component)
    {
        if (component.Actor != null)
        {
            throw new InvalidOperationException("An actor component cannot be set on more than one actor.");
        }
        
        if (RootComponent == null && component is SpatialComponent spatial)
        {
            RootComponent = spatial;
        }

        component.Actor = this;

#if DEBUG
        if (component is MeshComponent { IsVisible: false } mesh)
        {
            Components.Add(new DebugComponent(mesh.Descriptor.Bounds, new Vector3(1, 0, 1), 1, $"{mesh.Name} (Bounds)") { Relation = mesh });
            // Components.Add(new DebugComponent(mesh.Descriptor.Bounds, mesh.Descriptor.Bounds.Extents.Length(), new Vector3(1, 0.5f, 1), 1, $"{mesh.Name} (SphereRadius)") { Relation = mesh });
        }
#endif
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
            case NotifyCollectionChangedAction.Reset:
                RootComponent = null;
                break;
        }
    }
    
    internal void ComputeSelected()
    {
        var any = Components.Any(component => component.IsSelected);
        if (IsSelected == any) return;
        
        IsSelected = any;
    }
}
