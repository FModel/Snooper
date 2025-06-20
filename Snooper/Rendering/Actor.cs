using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering;

public class Actor
{
    public FGuid Guid { get; }
    public string Name { get; }
    public bool IsVisible { get; internal set; }

    public Actor(FGuid guid, string name, TransformComponent? transform = null)
    {
        Guid = guid;
        Name = name;
        IsVisible = true;

        Components = new ActorComponentCollection(this);
        Children = [];

        Children.CollectionChanged += OnChildrenCollectionChanged;
        Components.CollectionChanged += OnComponentsCollectionChanged;

        Transform = transform ?? new TransformComponent();
        InstancedTransforms = new InstancedTransformComponent();
        
        Components.Add(Transform);
        Components.Add(InstancedTransforms);
    }

    public ActorComponentCollection Components { get; }
    public ObservableCollection<Actor> Children { get; }

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
    public InstancedTransformComponent InstancedTransforms { get; }
    
    public void DrawInterface()
    {
        if (ImGui.TreeNode(Name))
        {
            if (Components.Count > 0)
            {
                ImGui.SeparatorText($"{Components.Count} Components");
                foreach (var component in Components)
                {
                    ImGui.Text($"- {component.GetType().Name}");
                }
            }

            if (Children.Count > 0)
            {
                ImGui.SeparatorText($"{Children.Count} Children");
                foreach (var child in Children)
                {
                    child.DrawInterface();
                }
            }

            ImGui.TreePop();
        }
    }

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
