using System.Numerics;

namespace Snooper.Rendering.Components.Transforms;

public class InstancedTransformComponent : ActorComponent
{
    public readonly List<Matrix4x4> LocalMatrices = []; // local matrices will be relative to the actor's relation matrix
    
    public void AddLocalInstance(TransformComponent transformComponent)
    {
        transformComponent.UpdateLocalMatrix();
        LocalMatrices.Add(transformComponent.LocalMatrix);
    }
}