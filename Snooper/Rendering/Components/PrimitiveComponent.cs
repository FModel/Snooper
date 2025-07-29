using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public class PrimitiveSection(int firstIndex, int indexCount)
{
    private static int _nextId = 0;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);
    
    public readonly int FirstIndex = firstIndex;
    public readonly int IndexCount = indexCount;

    public IndirectDrawMetadata DrawMetadata = new();
    public IDrawDataContainer? DrawDataContainer;
    
    public bool IsGenerated => DrawMetadata.BaseInstance >= 0;

    public override bool Equals(object? obj) => obj is PrimitiveSection section && section.SectionId.Equals(SectionId);
    public override int GetHashCode() => SectionId.GetHashCode();
}

public struct AABB
{
    public readonly Vector3 Center;
    public float DrawId;
    public readonly Vector3 Extents;
    public float InstanceCount;
    public float SectionCount;
    public float Padding1, Padding2, Padding3;

    public AABB(FBox box)
    {
        box *= Settings.GlobalScale;
        box.GetCenterAndExtents(out var center, out var extents);
        
        Center = new Vector3(center.X, center.Z, center.Y);
        Extents = new Vector3(extents.X, extents.Z, extents.Y);
    }
    
    public static implicit operator AABB(FBox box) => new(box);
}

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerDrawData>(TPrimitiveData<TVertex> primitive, AABB bounding)
    : ActorComponent, IControllableComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public TPrimitiveData<TVertex> Primitive { get; } = primitive;
    public abstract PrimitiveSection[] Sections { get; }
    
    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        if (!Primitive.IsValid)
            throw new InvalidOperationException("Primitive data is not valid.");
        
        resources.Add(Primitive, Sections, GetPerInstanceData(), bounding);
        textureManager.AddRange(Sections);
    }

    public virtual void Update(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
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
public class PrimitiveComponent<TVertex, TPerDrawData>(TPrimitiveData<TVertex> primitive, AABB bounding) : PrimitiveComponent<TVertex, PerInstanceData, TPerDrawData>(primitive, bounding)
    where TVertex : unmanaged
    where TPerDrawData : unmanaged, IPerDrawData
{
    public sealed override PrimitiveSection[] Sections { get; } = [new(0, primitive.Indices.Length)];
}

/// <inheritdoc />
public class PrimitiveComponent<TPerDrawData>(IPrimitiveData primitive) : PrimitiveComponent<Vector3, TPerDrawData>(primitive, new FBox()) where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : PrimitiveComponent<PerDrawData>(primitive);
