using System.Collections.Specialized;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using ImGuiNET;
using Snooper.Core.Managers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class Actor
{
    public string Name { get; }
    public string? ExportType { get; }
    public string? InternalType { get; }

    internal bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;

            _isSelected = value;
            IsOutlined = value;
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
            {
                component.IsVisible = field;
            }

            foreach (var child in Children)
            {
                child.IsVisible = field;
            }
        }
    } = true;

    public bool IsOutlined
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            OnOutlinedChanged?.Invoke();

            foreach (var child in Children)
            {
                child.IsOutlined = field;
            }
        }
    }

    internal event Action? OnOutlinedChanged;

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

    public ActorManager? ActorManager
    {
        get;
        internal set
        {
            if (field == value) return;

            if (field != null) OnSceneDetached(field);
            field = value;
            if (field != null) OnSceneAttached(field);
        }
    }

    public SpatialComponent? RootComponent
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            Icon = field?.Icon ?? Icon;
        }
    }

    internal readonly int _id = Random.Shared.Next();
    internal virtual string Icon { get; private set; } = "\uf1b2";

    public event Action<IGameSystem>? OnAttachedToScene;
    public event Action<IGameSystem>? OnDetachedFromScene;

    protected virtual void OnSceneAttached(IGameSystem scene)
    {
        OnAttachedToScene?.Invoke(scene);
    }
    protected virtual void OnSceneDetached(IGameSystem scene)
    {
        OnDetachedFromScene?.Invoke(scene);
    }

    private void AddChildrenInternal(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new InvalidOperationException("This actor already has a parent.");
        }

        actor._parent = this;
        actor.RootComponent?.Relation = RootComponent;
    }

    private void RemoveChildrenInternal(Actor actor)
    {
        if (actor.Parent != this)
        {
            throw new InvalidOperationException("This actor is not a child of the expected parent.");
        }

        actor._parent = null;
        actor.RootComponent?.Relation = null;
    }

    private void AddComponentInternal(ActorComponent component)
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

    private void RemoveComponentInternal(ActorComponent component)
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
                    AddChildrenInternal(actor);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var actor in e.OldItems!.Cast<Actor>())
                {
                    RemoveChildrenInternal(actor);
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
                    AddComponentInternal(component);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var component in e.OldItems!.Cast<ActorComponent>())
                {
                    RemoveComponentInternal(component);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                RootComponent = null;
                break;
        }
    }

    internal virtual void DrawInterface()
    {
        // ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiBold]);
        ImGui.TextUnformatted(Name);
        // ImGui.PopFont();
        if (ExportType != null)
        {
            ImGui.Text($"Export Type: {ExportType}");
        }
        if (InternalType != null)
        {
            ImGui.Text($"Internal Type: {InternalType}");
        }
    }
}
