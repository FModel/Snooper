using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports;
using Serilog;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Actors;

public class Actor : TreeNode
{
    private Actor(Actor other) : base(other)
    {
        Components = new ActorComponentCollection(this);
        Children = new ActorChildrenCollection(this);

        foreach (var component in other.Components)
        {
            Components.Add((ActorComponent) component.Clone());
        }

        foreach (var child in other.Children)
        {
            Children.Add((Actor) child.Clone());
        }
    }

    public Actor(string name) : base(name)
    {
        Components = new ActorComponentCollection(this);
        Children = new ActorChildrenCollection(this);
    }

    protected Actor(UObject actor) : base(actor)
    {
        Components = new ActorComponentCollection(this);
        Children = new ActorChildrenCollection(this);

        if (actor.TryGetValue(out bool hidden, "bHidden"))
        {
            IsVisible = !hidden;
        }
    }

    public ActorComponentCollection Components { get; }
    public ActorChildrenCollection Children { get; }

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

    public ActorManager? ActorManager { get; private set; }

    public uint Revision { get; private set; }

    internal void SetScene(ActorManager? manager, EEndPlayReason reason)
    {
        if (ActorManager == manager) return;

        EndPlayReason = reason;

        ActorManager?.UnregisterActor(this);
        ActorManager = manager;
        ActorManager?.RegisterActor(this);

        foreach (var component in Components.ToArray())
        {
            component.UpdatePlayState(reason);
        }

        foreach (var child in Children.ToArray())
        {
            child.SetScene(manager, reason);
        }

        EndPlayReason = EEndPlayReason.Destroyed;
    }

    internal EEndPlayReason EndPlayReason { get; private set; } = EEndPlayReason.Destroyed;

    public SpatialComponent? RootComponent
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            field?.IsNodeOpen = true;

            SetIcon(field?.Icon ?? Icon);
        }
    }

    public bool IsVisible
    {
        get;
        set
        {
            if (field == value) return;

            field = value;

            foreach (var component in Components.OfType<IPrimitiveComponent>())
                component.IsVisible = field;
            foreach (var child in Children)
                child.IsVisible = field;
        }
    } = true;

    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        foreach (var component in Components)
        {
            ct.ThrowIfCancellationRequested();
            component.Export(session, ct);
        }

        foreach (var child in Children)
        {
            ct.ThrowIfCancellationRequested();
            child.Export(session, ct);
        }
    }

    internal void IncrementRevision() => Revision++;

    public bool IsDescendantOf(Actor other)
    {
        for (var current = _parent; current != null; current = current._parent)
        {
            if (current == other) return true;
        }
        return false;
    }

    public bool AttachTo(Actor newParent, SpatialComponent? attachTo = null, string? socket = null, bool keepWorldTransform = true)
    {
        if (!CanAttachTo(newParent, attachTo, out var relation)) return false;

        if (_parent != newParent) MoveUnder(newParent);
        RootComponent?.AttachTo(relation, socket, keepWorldTransform);

        Log.Verbose("{Actor} attached to {Target}", Name, relation?.Name ?? newParent.Name);
        return true;
    }

    private bool CanAttachTo(Actor newParent, SpatialComponent? attachTo, out SpatialComponent? relation)
    {
        relation = attachTo ?? newParent.RootComponent;

        if (newParent == this)
        {
            Log.Warning("{Actor} cannot be attached to itself", Name);
            return false;
        }
        if (newParent.IsDescendantOf(this))
        {
            Log.Warning("{Actor} cannot be attached to {Target}, which is already below it", Name, newParent.Name);
            return false;
        }
        if (_parent is null)
        {
            Log.Warning("{Actor} has no parent to be moved away from", Name);
            return false;
        }
        if (RootComponent is { } root && relation is not null && relation.IsAttachedTo(root))
        {
            Log.Warning("{Actor} cannot be attached to {Target}, which already hangs off its root", Name, relation.Name);
            return false;
        }

        return true;
    }

    private void MoveUnder(Actor newParent)
    {
        var oldParent = _parent!;
        var manager = ActorManager;

        if (manager is null || newParent.ActorManager != manager)
        {
            // this actor is not yet part of the scene, or the new parent is in a different scene somehow
            oldParent.Children.Remove(this);
            newParent.Children.Add(this);
            return;
        }

        oldParent.Children.RemoveQuiet(this);
        newParent.Children.AddQuiet(this);

        _parent = newParent;
        UpdateHierarchyDepth();

        manager.IncrementRevision();
    }

    public bool Detach(bool keepWorldTransform = true)
    {
        if (ActorManager is not SceneManager { RootActor: { } root })
        {
            Log.Warning("{Actor} has no scene root to be detached to", Name);
            return false;
        }

        return root != this && AttachTo(root, root.RootComponent, null, keepWorldTransform);
    }

    internal void OnChildAdded(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new InvalidOperationException("This actor already has a parent.");
        }

        actor._parent = this;
        actor.RootComponent?.Relation = RootComponent;
        actor.UpdateHierarchyDepth();

        actor.SetScene(ActorManager, EEndPlayReason.Destroyed);
    }

    internal void OnChildRemoved(Actor actor)
    {
        if (actor.Parent != this)
        {
            throw new InvalidOperationException("This actor is not a child of the expected parent.");
        }

        actor.SetScene(null, EEndPlayReason.Destroyed);

        actor._parent = null;
        actor.RootComponent?.Relation = null;
        actor.UpdateHierarchyDepth();
    }

    internal void OnComponentAdded(ActorComponent component)
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
    }

    internal void OnComponentRemoved(ActorComponent component)
    {
        if (component.Actor != this)
        {
            throw new InvalidOperationException("This actor component is not set on this actor.");
        }

        if (RootComponent == component)
        {
            RootComponent = null;
        }

        var relation = (component as SpatialComponent)?.Relation;
        component.Actor = null;

        if (component is SpatialComponent spatial)
        {
            foreach (var child in spatial.Children.ToArray())
            {
                child.Relation = relation;
            }
        }
    }

    public override void SetOutlined(bool state)
    {
        foreach (var c in Components)
            c.SetOutlined(state);

        foreach (var child in Children)
            child.SetOutlined(state);
    }
    public override bool ShouldScrollHere
    {
        get;
        set
        {
            field = value;
            Parent?.ShouldScrollHere = field;

            if (field) IsNodeOpen = true;
        }
    }
    private void UpdateHierarchyDepth()
    {
        NodeDepth = (_parent?.NodeDepth ?? -1) + 1;
        foreach (var child in Children)
            child.UpdateHierarchyDepth();
    }
    public override void DrawControls()
    {

    }

    public sealed override object Clone() => new Actor(this);
}
