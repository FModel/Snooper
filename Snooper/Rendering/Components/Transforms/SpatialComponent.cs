using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

public interface ISpatialComponent
{
    public Matrix4x4 WorldMatrix { get; }
    public Matrix4x4 LocalMatrix { get; }
    public Transform LocalTransform { get; }
    
    void UpdateWorldMatrix(bool recursive = true);
    void AttachTo(ISpatialComponent parent);
}

[DefaultActorSystem(typeof(TransformSystem))]
public class SpatialComponent<TInstanceData>(Transform? transform = null, string? name = null) : ActorComponent(name), ISpatialComponent, IControllable where TInstanceData : unmanaged, IPerInstanceData
{
    public List<Transform> LocalInstanceTransforms = [transform ?? Transform.Identity];

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

    public ISpatialComponent? Relation;
    public void AttachTo(ISpatialComponent parent)
    {
        if (Relation is not null)
            throw new InvalidOperationException("This component is already attached to a parent.");
        
        Relation = parent;
    }
    
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
    
    private TInstanceData[]? _cachedInstanceData { get; set; }
    public TInstanceData[] GetPerInstanceData()
    {
        Relation?.UpdateWorldMatrix();
        var relation = Relation?.WorldMatrix ?? Matrix4x4.Identity;
        var data = new TInstanceData[LocalInstanceTransforms.Count];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = new TInstanceData { Matrix = LocalInstanceTransforms[i].ToMatrix() * relation };
        }
        
        if (_cachedInstanceData is null)
        {
            if (ApplyInstanceData(data))
                _cachedInstanceData = data;
        }
        else
        {
            CopyCachedData(data, _cachedInstanceData);
        }

        return data;
    }
    protected virtual bool ApplyInstanceData(TInstanceData[] data)
    {
        return false;
    }
    protected virtual void CopyCachedData(TInstanceData[] data, TInstanceData[] cached)
    {
        
    }
    
    public virtual void DrawControls()
    {
        if (ImGui.TreeNode("Transform"))
        {
            if (ImGui.DragFloat3("Position", ref LocalTransform.Position, 0.1f))
            {
                UpdateLocalMatrix();
            }
            // if (ImGui.DragFloat4("Rotation", ref LocalTransform.Rotation, 0.1f))
            // {
            //     UpdateLocalMatrix();
            // }
            if (ImGui.DragFloat3("Scale", ref LocalTransform.Scale, 0.1f, 0.01f))
            {
                UpdateLocalMatrix();
            }

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

public class SpatialComponent(Transform? transform = null, string? name = null)
    : SpatialComponent<PerInstanceData>(transform, name);