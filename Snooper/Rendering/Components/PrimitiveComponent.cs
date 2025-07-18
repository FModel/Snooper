using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

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
    : ActorComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public abstract PrimitiveSection[] Sections { get; protected init; }
    
    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources)
    {
        if (!primitive.IsValid) throw new InvalidOperationException("Primitive data is not valid.");
        resources.Add(primitive, Sections, GetPerInstanceData());
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
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3, PerInstanceData, PerDrawData>(primitive)
{
    public sealed override PrimitiveSection[] Sections { get; protected init; } = [new(0, primitive.Indices.Length)];
}
