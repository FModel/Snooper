using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

[DefaultActorSystem(typeof(TransformSystem))]
public class SpatialComponent : ActorComponent, IControllable
{
    public SpatialComponent(Transform? transform = null, string? name = null) : base(name)
    {
        LocalTransform = transform ?? Transform.Identity;
    }
    
    public SpatialComponent(USceneComponent component) : base(component)
    {
        LocalTransform = component.GetRelativeTransform();
        
        _absPosition = component.GetOrDefault<bool>("bAbsoluteLocation");
        _absRotation = component.GetOrDefault<bool>("bAbsoluteRotation");
        _absScale = component.GetOrDefault<bool>("bAbsoluteScale");
    }
    
    private readonly bool _absPosition;
    private readonly bool _absRotation;
    private readonly bool _absScale;
    
    private Transform _localTransform = Transform.Identity;
    public Transform LocalTransform
    {
        get => _localTransform;
        set
        {
            if (_localTransform == value)
                return;
            
            _localTransform = value;
            MarkDirty();
        }
    }
    
    private Matrix4x4 _worldMatrix = Matrix4x4.Identity;
    public Matrix4x4 WorldMatrix
    {
        get => _worldMatrix;
        private set
        {
            if (_worldMatrix == value)
                return;
            
            _worldMatrix = value;
            MarkDirty();
        }
    }
    
    private SpatialComponent? _relation;
    public SpatialComponent? Relation
    {
        get => _relation;
        set
        {
            if (_relation == value) return;

            _relation?.Children.Remove(this);
            _relation = value;

            if (_relation != null && !_relation.Children.Contains(this))
                _relation.Children.Add(this);

            MarkDirty();
        }
    }
    
    public readonly List<SpatialComponent> Children = [];
    
    public virtual Matrix4x4[] GetInstanceMatrices() => [WorldMatrix];
    
    public void UpdateWorldMatrix(bool recursive = true)
    {
        if (Relation is null)
        {
            WorldMatrix = LocalTransform.ToMatrix();
        }
        else
        {
            if (recursive) Relation.UpdateWorldMatrix();
            if (!_absPosition && !_absRotation && !_absScale)
            {
                WorldMatrix = LocalTransform.ToMatrix() * Relation.WorldMatrix;
                return;
            }
            
            Matrix4x4.Decompose(Relation.WorldMatrix, out var scale, out var rotation, out _);
            
            WorldMatrix = new Transform
            {
                Position = _absPosition ? LocalTransform.Position : Vector3.Transform(LocalTransform.Position, Relation.WorldMatrix),
                Rotation = _absRotation ? LocalTransform.Rotation : rotation * LocalTransform.Rotation,
                Scale = _absScale ? LocalTransform.Scale : LocalTransform.Scale * scale
            }.ToMatrix();
        }
    }

    internal override void MarkDirty()
    {
        base.MarkDirty();
        
        foreach (var child in Children)
        {
            child.MarkDirty();
        }
    }

    public virtual void DrawControls()
    {
        EditorUI.CollapsingTable("Transform", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.DragFloat3("Position", ref LocalTransform.Position);
            EditorUI.DragFloat4("Rotation", ref LocalTransform.Rotation);
            EditorUI.DragFloat3("Scale", ref LocalTransform.Scale, 0.1f, 0.01f);

            if (Relation is ActorComponent relation)
            {
                EditorUI.Text("Attached To", $"{relation.Name} in {(relation.Actor == Actor ? "Self" : relation.Actor?.Name ?? "Unknown")}");
            }
        });
    }
}