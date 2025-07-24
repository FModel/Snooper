using System.Collections.Specialized;
using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering;

public class Actor
{
    public FGuid Guid { get; }
    public string Name { get; }
    public bool IsDirty { get; private set; } // currently driven by the mesh being in/out of view, or by the transform being changed
    
    private Range _visibleInstances = new(0, 1);
    public Range VisibleInstances
    {
        get => _visibleInstances;
        set 
        {
            if (_visibleInstances.Equals(value))
                return;
            
            _visibleInstances = value;
            MarkDirty();
        }
    }
    
    public bool IsVisible => VisibleInstances.Start.Value != VisibleInstances.End.Value;

    public Actor(FGuid guid, string name, TransformComponent? transform = null)
    {
        Guid = guid;
        Name = name;

        Components = new ActorComponentCollection(this);
        Children = new ActorChildrenCollection();

        Children.CollectionChanged += OnChildrenCollectionChanged;
        Components.CollectionChanged += OnComponentsCollectionChanged;

        Transform = transform ?? new TransformComponent();
        InstancedTransform = new InstancedTransformComponent();
        
        Components.Add(Transform);
        Components.Add(InstancedTransform);
    }

    public ActorComponentCollection Components { get; }
    public ActorChildrenCollection Children { get; }

    private Actor? _parent;
    public Actor? Parent
    {
        get => _parent;
        set
        {
            var oldParent = _parent;
            if (oldParent == value) return;

            oldParent?.Children.Remove(this);
            value?.Children.Add(this);
        }
    }

    public ActorManager? ActorManager { get; internal set; }
    public TransformComponent Transform { get; private set; }
    public InstancedTransformComponent InstancedTransform { get; }
    
    public Matrix4x4[] GetWorldMatrices()
    {
        var matrices = new Matrix4x4[1 + InstancedTransform.Transforms.Count];
        matrices[0] = Transform.WorldMatrix;
        for (var i = 0; i < InstancedTransform.Transforms.Count; i++)
        {
            matrices[i + 1] = GetInstanceWorldMatrix(i);
        }
        return matrices;
    }

    private Matrix4x4 GetInstanceWorldMatrix(int index)
    {
        if (index < 0 || index >= InstancedTransform.Transforms.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range of the instanced transforms.");
        }
        
        var instance = InstancedTransform.Transforms[index];
        
        Matrix4x4 relation;
        if (instance.ParentInstanceIndex >= 0 && Parent is not null)
        {
            relation = Parent.GetInstanceWorldMatrix(instance.ParentInstanceIndex);
        }
        else if (Transform.Relation is not null)
        {
            relation = Transform.Relation.WorldMatrix;
        }
        else
        {
            relation = Matrix4x4.Identity;
        }
        
        return instance.LocalMatrix * relation;
    }
    
    internal void MarkDirty() => IsDirty = true;
    internal void MarkClean() => IsDirty = false;

    internal readonly int Id = Random.Shared.Next();
    internal virtual string Icon => "cube";

    private void AddInternal(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new InvalidOperationException("This actor already has a parent.");
        }
        
        actor._parent = this;
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

        if (component is TransformComponent transformComponent)
        {
            Transform = transformComponent;
        }

        component.Actor = this;
    }

    private void RemoveInternal(ActorComponent component)
    {
        if (component.Actor != this)
        {
            throw new InvalidOperationException("This actor component is not set on this actor.");
        }

        if (component is TransformComponent)
        {
            if (Components.OfType<TransformComponent?>().FirstOrDefault() is null)
            {
                throw new InvalidOperationException("An actor always has to have a transform component.");
            }
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
