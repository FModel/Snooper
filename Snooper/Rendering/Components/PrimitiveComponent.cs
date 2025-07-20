using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
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

public abstract class TPrimitiveComponent<TVertex, TInstanceData, TPerDrawData>(TPrimitiveData<TVertex> primitive)
    : ActorComponent, IControllableComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public TPrimitiveData<TVertex> Primitive { get; } = primitive;
    public abstract PrimitiveSection[] Sections { get; }
    
    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources)
    {
        if (!Primitive.IsValid) throw new InvalidOperationException("Primitive data is not valid.");
        resources.Add(Primitive, Sections, GetPerInstanceData());
    }

    public virtual void Update(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources)
    {
        if (!Sections[0].IsGenerated)
        {
            Generate(resources);
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

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3, PerInstanceData, PerDrawData>(primitive)
{
    public sealed override PrimitiveSection[] Sections { get; } = [new(0, primitive.Indices.Length)];
}
