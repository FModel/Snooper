using System.Numerics;

namespace Snooper.Rendering;

public abstract class ActorComponent
{
    public Actor? Actor;
    public bool IsEnabled;

    public Matrix4x4 GetModelMatrix()
    {
        if (Actor == null)
            throw new InvalidOperationException("Actor is not set for this component.");

        return Actor.Transform.WorldMatrix;
    }
}
