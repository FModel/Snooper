using System.Numerics;
using ImGuiNET;
using Snooper.Core;
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
            Actor?.MarkDirty();
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
            Actor?.MarkDirty();
        }
    }

    public SpatialComponent? Relation;
    
    public void UpdateWorldMatrix(bool recursive = true)
    {
        UpdateLocalMatrix();
        UpdateWorldMatrixInternal(recursive);
    }
    
    public void UpdateLocalMatrix()
    {
        LocalMatrix = LocalTransform.ToMatrix();
    }
    
    internal void UpdateWorldMatrixInternal(bool recursive)
    {
        if (Relation is null)
        {
            WorldMatrix = LocalMatrix;
        }
        else
        {
            if (recursive) Relation.UpdateWorldMatrix();
            WorldMatrix = LocalMatrix * Relation.WorldMatrix;
        }
    }
    
    public void DrawControls()
    {
        // if (ImGui.DragFloat3("Position", ref LocalTransform.Position, 0.1f))
        // {
        //     UpdateLocalMatrix();
        // }
        // if (ImGui.DragFloat4("Rotation", ref LocalTransform.Rotation, 0.1f))
        // {
        //     UpdateLocalMatrix();
        // }
        // if (ImGui.DragFloat3("Scale", ref LocalTransform.Scale, 0.1f, 0.01f))
        // {
        //     UpdateLocalMatrix();
        // }
    }
}