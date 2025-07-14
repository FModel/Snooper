using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<TVertex, TInstanceData>(TPrimitiveData<TVertex> primitive)
    : ActorComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
{
    public IndirectDrawMetadata DrawMetadata { get; private set; } = new();
    public abstract MeshMaterialSection[] MaterialSections { get; protected init; }

    public void Generate(IndirectResources<TVertex, TInstanceData> resources)
    {
        if (!primitive.IsValid) return;
        DrawMetadata = resources.Add(primitive, MaterialSections, GetPerInstanceData());
    }

    public virtual void Update(IndirectResources<TVertex, TInstanceData> resources)
    {
        if (DrawMetadata.BaseInstance < 0)
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
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3, PerInstanceData>(primitive)
{
    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; } = [new(0, 0, primitive.Indices.Length)];
}
