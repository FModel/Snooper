using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports;
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
