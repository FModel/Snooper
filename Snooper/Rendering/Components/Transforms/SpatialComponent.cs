using System.Numerics;
using ImGuiNET;
using Serilog;
using Snooper.Core;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

[DefaultActorSystem(typeof(TransformSystem))]
public class SpatialComponent(Transform? transform = null, string? name = null) : ActorComponent(name), IControllable
{
    public readonly List<Transform> LocalInstanceTransforms = [transform ?? Transform.Identity];

    public Transform LocalTransform
    {
        get => LocalInstanceTransforms[0];
        set => LocalInstanceTransforms[0] = value;
    }
    
    private Matrix4x4 _localMatrix = Matrix4x4.Identity;
    public Matrix4x4 LocalMatrix
    {
        get => _localMatrix;
        private set
        {
            if (_localMatrix == value)
                return;
            
            _localMatrix = value;
            MarkDirty();
        }
    }

    public bool UseAbsolutePosition { get; set; }
    public bool UseAbsoluteRotation { get; set; }
    public bool UseAbsoluteScale { get; set; }

    private Transform? _worldTransform = null;
    public Transform WorldTransform
    {
        get
        {
            if (_worldTransform != null)
                return _worldTransform;
            UpdateWorldTransform();
            return _worldTransform ?? LocalTransform;
        }
        private set => _worldTransform = value;
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
            if (this is InstancedStaticMeshComponent && value is InstancedStaticMeshComponent)
            {
                Log.Warning("InstancedStaticMeshComponent cannot be used as a relation to another InstancedStaticMeshComponent");
                return;
            }
            if (_relation == value) return;

            _relation?.Children.Remove(this);
            _relation = value;

            if (_relation != null && !_relation.Children.Contains(this))
                _relation.Children.Add(this);

            MarkDirty();
        }
    }
    
    public readonly List<SpatialComponent> Children = [];
    
    public void UpdateWorldMatrix(bool recursive = true)
    {
        UpdateLocalMatrix();
        UpdateWorldMatrixInternal(recursive);
    }
    
    private void UpdateLocalMatrix()
    {
        LocalMatrix = LocalTransform.ToMatrix();
    }

    protected Transform BuildWorldTransform(Transform localTransform)
    {
        if (Relation is null)
        {
            return LocalTransform;
        }
        else
        {
            var ret = new Transform();

            if (UseAbsoluteRotation)
                ret.Rotation = localTransform.Rotation;
            else
                ret.Rotation = Relation.WorldTransform.Rotation * localTransform.Rotation;

            if (UseAbsoluteScale)
                ret.Scale = localTransform.Scale;
            else
                ret.Scale = localTransform.Scale * Relation.WorldTransform.Scale;

            if (UseAbsolutePosition)
            {
                ret.Position = localTransform.Position;
            }
            else
            {
                Relation.UpdateWorldMatrix();
                ret.Position = Vector3.Transform(localTransform.Position, Relation.WorldMatrix);
            }

            return ret;
        }
    }

    private void UpdateWorldMatrixInternal(bool recursive)
    {
        if (Relation is null)
        {
            WorldMatrix = LocalMatrix;
        }
        else
        {
            if (recursive) Relation.UpdateWorldMatrix();

            if (UseAbsoluteRotation || UseAbsoluteScale || UseAbsolutePosition)
            {
                WorldMatrix = WorldTransform.ToMatrix();
            }
            else
            {
                WorldMatrix = LocalMatrix * Relation.WorldMatrix;
            }
        }
    }

    internal override void MarkDirty()
    {
        _worldTransform = null;

        base.MarkDirty();
        
        foreach (var child in Children)
        {
            child.MarkDirty();
        }
    }

    public virtual void DrawControls()
    {
        if (ImGui.TreeNode("Transform"))
        {
            ImGui.DragFloat3("Position", ref LocalTransform.Position, 0.1f);
            // ImGui.DragFloat4("Rotation", ref LocalTransform.Rotation, 0.1f);
            ImGui.DragFloat3("Scale", ref LocalTransform.Scale, 0.1f, 0.01f);

            ImGui.Text($"Instances: {LocalInstanceTransforms.Count}");
            if (Relation is ActorComponent relation)
            {
                ImGui.Text($"Attached to: {relation.DisplayName}");
                ImGui.Text($"Owner: {(relation.Actor == Actor ? "Self" : relation.Actor?.Name)}");
            }
            
            ImGui.TreePop();
        }
    }
}