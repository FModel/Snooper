using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    public int DrawId { get; private set; } = -1;

    public void Generate(IndirectResources<T> resources)
    {
        DrawId = resources.Add(primitive, GetWorldMatrices());
    }

    public virtual void Update(IndirectResources<T> resources)
    {
        if (DrawId < 0)
        {
            Generate(resources);
        }
        else
        {
            resources.Update(this);
        }
    }
    
    public Matrix4x4 GetModelMatrix()
    {
        if (Actor == null)
            throw new InvalidOperationException("Actor is not set for this component.");

        return Actor.Transform.WorldMatrix;
    }

    private Matrix4x4[] GetWorldMatrices()
    {
        var matrices = new Matrix4x4[1 + Actor.InstancedTransforms.WorldMatrix.Count];
        matrices[0] = GetModelMatrix();
        for (var i = 0; i < Actor.InstancedTransforms.WorldMatrix.Count; i++)
        {
            matrices[i + 1] = Actor.InstancedTransforms.WorldMatrix[i];
        }
        return matrices;
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive);
