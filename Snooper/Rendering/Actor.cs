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
        InstancedTransforms = new InstancedTransformComponent();
        
        Components.Add(Transform);
        Components.Add(InstancedTransforms);
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
    public InstancedTransformComponent InstancedTransforms { get; }
    
    public Matrix4x4[] GetWorldMatrices()
    {
        // TODO: this is called a lot, find a way to optimize it
        var relation = Transform.Relation?.WorldMatrix ?? Matrix4x4.Identity;
        var matrices = new Matrix4x4[1 + InstancedTransforms.LocalMatrices.Count];
        matrices[0] = Transform.WorldMatrix;
        for (var i = 0; i < InstancedTransforms.LocalMatrices.Count; i++)
        {
            matrices[i + 1] = InstancedTransforms.LocalMatrices[i] * relation;
        }
        return matrices;
    }
    
    internal void MarkDirty() => IsDirty = true;
    internal void MarkClean() => IsDirty = false;

    private const int FrameLimit = 200;
    private int _frameCounter;
    private bool _condition;
    
    private readonly int _id = Random.Shared.Next();
    private readonly Vector2 _iconSize = new(10);
    internal void DrawInterface()
    {
        ImGui.PushID(_id);
        
        ImGui.Image(0, _iconSize, Vector2.UnitY, Vector2.UnitX, Vector4.Zero, Vector4.One);
        ImGui.SameLine();

        if (!_condition && IsDirty) _condition = true;
        if (_condition)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0f, 0f, 1));
            _frameCounter++;
        }
        var open = ImGui.TreeNodeEx(Name, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick);
        if (_condition) ImGui.PopStyleColor();

        if (_frameCounter >= FrameLimit)
        {
            _frameCounter = 0;
            _condition = false;
        }
        
        if (open)
        {
            ImGui.Text($"Visible Instances: {VisibleInstances}");
            
            Transform.DrawInterface();
            
            // if (Components.Count > 0)
            // {
            //     ImGui.SeparatorText($"{Components.Count} Components");
            //     foreach (var component in Components)
            //     {
            //         ImGui.Text($"- {component.GetType().Name}");
            //     }
            // }
            
            var count = Children.Count;
            if (count > 0)
            {
                ImGui.SeparatorText($"{count} Child{(count > 1 ? "ren" : "")}");
                foreach (var child in Children)
                {
                    child.DrawInterface();
                }
            }
            
            ImGui.TreePop();
        }
        
        ImGui.PopID();
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
