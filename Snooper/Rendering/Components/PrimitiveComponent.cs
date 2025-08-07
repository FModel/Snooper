using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerDrawData> : ActorComponent, IControllableComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public LevelOfDetail<TVertex>[] LevelOfDetails;
    public readonly CullingBounds Bounds;
    public readonly MaterialSection[] Materials; // we store materials for each section at lod 0

    protected PrimitiveComponent(LevelOfDetail<TVertex>[] levelOfDetails, CullingBounds bounds)
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
        
        Array.Clear(LevelOfDetails);
        LevelOfDetails = null; // TODO: keep some info for imgui
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
        if (Actor is null)
            throw new InvalidOperationException("Actor is not set for the component.");
        
        var matrices = Actor.GetWorldMatrices();
        var data = new TInstanceData[matrices.Length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = new TInstanceData { Matrix = matrices[i] };
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
        // ImGui.Text($"Vertices: {LevelOfDetails[0].Primitive.Vertices.Length}");
        // ImGui.Text($"Indices: {LevelOfDetails[0].Primitive.Indices.Length}");
        //
        // ImGui.SeparatorText("Descriptors");
        // ImGui.Text($"Level of Detail Count: {LevelOfDetails.Length}");
        // for (var i = 0; i < LevelOfDetails.Length; i++)
        // {
        //     ImGui.Text($"LOD {i}: {LevelOfDetails[i].Primitive.Vertices.Length} Vertices, {LevelOfDetails[i].Primitive.Indices.Length} Indices");
        //     ImGui.Text($"Section Count: {LevelOfDetails[i].SectionDescriptors.Length}");
        //     foreach (var section in LevelOfDetails[i].SectionDescriptors)
        //     {
        //         ImGui.Text($"First Index: {section.FirstIndex}");
        //         ImGui.Text($"Index Count: {section.IndexCount}");
        //         ImGui.Text($"Material Index: {section.MaterialIndex}");
        //     }
        //     ImGui.Separator();
        // }

        // ImGui.SeparatorText($"{Sections.Length} Section{(Sections.Length > 1 ? "s" : "")}");
        // foreach (var section in Sections)
        // {
        //     ImGui.Text($"DrawID {section.DrawMetadata.DrawId}");
        //     ImGui.Text($"Material Index: {section.MaterialIndex}");
        //     if (section.DrawDataContainer is not null)
        //     {
        //         section.DrawDataContainer.DrawControls();
        //     }
        //     else
        //     {
        //         ImGui.Text("No draw data container available.");
        //     }
        // }
    }
}

/// <summary>
/// primitive component that uses a single section for the entire primitive data.
/// </summary>
public class PrimitiveComponent<TVertex, TPerDrawData>(TPrimitiveData<TVertex> primitive, CullingBounds bounds)
    : PrimitiveComponent<TVertex, PerInstanceData, TPerDrawData>([new LevelOfDetail<TVertex>(primitive)], bounds)
    where TVertex : unmanaged
    where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
public class PrimitiveComponent<TPerDrawData>(IPrimitiveData primitive, CullingBounds bounds)
    : PrimitiveComponent<Vector3, TPerDrawData>(primitive, bounds)
    where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : PrimitiveComponent<PerDrawData>(primitive, new FBox());
