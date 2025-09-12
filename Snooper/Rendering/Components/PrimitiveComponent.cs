using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerDrawData> : SpatialComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public readonly LevelOfDetail<TVertex>[] LevelOfDetails;
    public readonly CullingBounds Bounds;
    public readonly MaterialSection[] Materials; // we store materials for each section at lod 0

    public bool IsTranslucent => Materials.Any(m => m.IsTranslucent); // TODO: this is delayed by tasks

    protected PrimitiveComponent(LevelOfDetail<TVertex>[] levelOfDetails, CullingBounds bounds, Transform? transform = null, string? name = null) : base(transform, name)
    {
        LevelOfDetails = levelOfDetails;
        Bounds = bounds;
        Materials = new MaterialSection[levelOfDetails[0].SectionDescriptors.Length];
        for (var i = 0; i < Materials.Length; i++)
        {
            Materials[i] = new MaterialSection(levelOfDetails[0].SectionDescriptors[i].MaterialIndex);
        }
    }
    
    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        resources.Add(LevelOfDetails, Materials, GetPerInstanceData(), Bounds);
        textureManager.AddRange(Materials);
    }

    public void Update(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        if (!Materials[0].IsGenerated)
        {
            Generate(resources, textureManager);
        }
        else
        {
            resources.Update(this);
        }
    }
    
    private TInstanceData[]? _cachedInstanceData { get; set; }
    public TInstanceData[] GetPerInstanceData()
    {
        var relation = Relation?.WorldMatrix ?? Matrix4x4.Identity;
        var data = new TInstanceData[LocalInstanceTransforms.Count];
        for (var i = 0; i < data.Length; i++)
        {
            Matrix4x4 instanceMatr;
            if (Relation != null && (UseAbsolutePosition || UseAbsoluteRotation || UseAbsoluteScale))
            {
                instanceMatr = BuildWorldTransform(LocalInstanceTransforms[i]).ToMatrix();
            }
            else
            {
                instanceMatr = LocalInstanceTransforms[i].ToMatrix() * relation;
            }
            data[i] = new TInstanceData { Matrix = instanceMatr };
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

    public override void DrawControls()
    {
        base.DrawControls();
        
        if (ImGui.TreeNode("Primitive"))
        {
            ImGui.Text($"LODs: {LevelOfDetails.Length}");
            ImGui.Text($"Sections: {Materials.Length}");
            ImGui.Text($"Bounds: {Bounds}");
            
            ImGui.TreePop();
        }
    }
}

/// <summary>
/// primitive component that uses a single section for the entire primitive data.
/// </summary>
public class PrimitiveComponent<TVertex, TPerDrawData>(TPrimitiveData<TVertex> primitive, CullingBounds bounds, Transform? transform = null, string? name = null)
    : PrimitiveComponent<TVertex, PerInstanceData, TPerDrawData>([new LevelOfDetail<TVertex>(FGuid.Random(), primitive)], bounds, transform, name)
    where TVertex : unmanaged
    where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
public class PrimitiveComponent<TPerDrawData>(PrimitiveData primitive, CullingBounds bounds, Transform? transform = null, string? name = null)
    : PrimitiveComponent<Vector3, TPerDrawData>(primitive, bounds, transform, name)
    where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(PrimitiveData primitive, Transform? transform = null, string? name = null) : PrimitiveComponent<PerDrawData>(primitive, new FBox(), transform, name);
