using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    public IndirectDrawMetadata DrawMetadata { get; private set; } = new();
    public abstract MeshMaterialSection[] MaterialSections { get; protected init; }

    public virtual void Generate(IndirectResources<T> resources)
    {
        if (!primitive.IsValid) return;
        DrawMetadata = resources.Add(primitive, MaterialSections, Actor.GetWorldMatrices());
    }

    public virtual void Update(IndirectResources<T> resources)
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
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive)
{
    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; } = [new(0, 0, primitive.Indices.Length)];
}
