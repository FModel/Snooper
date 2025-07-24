using System.Numerics;

namespace Snooper.Rendering.Components.Transforms;

/// <summary>
/// this struct is used to represent an instanced transform
/// this instanced transform is a local matrix that is relative to its parent actor's transform
/// if <see cref="ParentInstanceIndex"/> is >= 0, then the local matrix is relative to the instance at that index in the parent actor's instanced transform
/// </summary>
/// <param name="localMatrix"></param>
/// <param name="parentInstanceIndex">index of the instance to use (in the parent actor) as this local matrix's relation</param>
public readonly struct InstancedTransform(Matrix4x4 localMatrix, int parentInstanceIndex = -1)
{
    public readonly Matrix4x4 LocalMatrix = localMatrix;
    public readonly int ParentInstanceIndex = parentInstanceIndex;
}

public class InstancedTransformComponent : ActorComponent
{
    public readonly List<InstancedTransform> Transforms = [];
    
    public void AddLocalInstance(TransformComponent transformComponent)
    {
        transformComponent.UpdateLocalMatrix();
        Transforms.Add(new InstancedTransform(transformComponent.LocalMatrix));
    }
}