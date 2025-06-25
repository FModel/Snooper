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

    public void Generate(IndirectResources<T> resources)
    {
        DrawMetadata = resources.Add(primitive, MaterialSections, GetWorldMatrices());
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

    public Matrix4x4[] GetWorldMatrices()
    {
        if (Actor == null)
            throw new InvalidOperationException("Actor is not set for this component.");
        
        var matrices = new Matrix4x4[1 + Actor.InstancedTransforms.WorldMatrix.Count];
        matrices[0] = Actor.Transform.WorldMatrix;
        for (var i = 0; i < Actor.InstancedTransforms.WorldMatrix.Count; i++)
        {
            matrices[i + 1] = Actor.InstancedTransforms.WorldMatrix[i];
        }
        return matrices;
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive)
{
    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; } = [new(0, 0, primitive.Indices.Length)];
}
