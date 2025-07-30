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

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerDrawData>(TPrimitiveData<TVertex> primitive, CullingBounds bounds)
    : ActorComponent, IControllableComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public TPrimitiveData<TVertex> Primitive { get; } = primitive;
    public CullingBounds Bounds { get; } = bounds;
    public abstract PrimitiveSection[] Sections { get; }
    
    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        if (!Primitive.IsValid)
            throw new InvalidOperationException("Primitive data is not valid.");
        
        resources.Add(Primitive, Sections, GetPerInstanceData(), Bounds);
        textureManager.AddRange(Sections);
    }

    public void Update(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        if (!Sections[0].IsGenerated)
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
        ImGui.Text($"Vertices: {Primitive.Vertices.Length}");
        ImGui.Text($"Indices: {Primitive.Indices.Length}");
        
        ImGui.SeparatorText($"{Sections.Length} Section{(Sections.Length > 1 ? "s" : "")}");
        foreach (var section in Sections)
        {
            ImGui.Text($"DrawID {section.DrawMetadata.DrawId}, FirstIndex: {section.FirstIndex}, IndexCount: {section.IndexCount}");
            if (section.DrawDataContainer is not null)
            {
                section.DrawDataContainer.DrawControls();
            }
            else
            {
                ImGui.Text("No draw data container available.");
            }
        }
    }
}

/// <summary>
/// primitive component that uses a single section for the entire primitive data.
/// </summary>
public class PrimitiveComponent<TVertex, TPerDrawData>(TPrimitiveData<TVertex> primitive, CullingBounds bounds) : PrimitiveComponent<TVertex, PerInstanceData, TPerDrawData>(primitive, bounds)
    where TVertex : unmanaged
    where TPerDrawData : unmanaged, IPerDrawData
{
    public sealed override PrimitiveSection[] Sections { get; } = [new(0, primitive.Indices.Length)];
}

/// <inheritdoc />
public class PrimitiveComponent<TPerDrawData>(IPrimitiveData primitive, CullingBounds bounds) : PrimitiveComponent<Vector3, TPerDrawData>(primitive, bounds) where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : PrimitiveComponent<PerDrawData>(primitive, new FBox());
