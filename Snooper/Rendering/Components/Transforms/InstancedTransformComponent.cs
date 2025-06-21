using System.Numerics;

namespace Snooper.Rendering.Components.Transforms;

public class InstancedTransformComponent : ActorComponent
{
    public readonly List<Matrix4x4> WorldMatrix = [];
    
    public bool IsInstanced => WorldMatrix.Count > 0;
    
    public void AddInstance(TransformComponent transformComponent)
    {
        transformComponent.UpdateLocalMatrix();
        WorldMatrix.Add(transformComponent.LocalMatrix);
    }
}