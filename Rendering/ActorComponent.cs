using System.Numerics;

namespace Snooper.Rendering;

public abstract class ActorComponent
{
    public Actor? Actor;
    public bool IsEnabled;
    public int DrawId = -1;

    public bool IsVisible => Actor?.IsVisible ?? false;

    public Matrix4x4 GetModelMatrix()
    {
        if (Actor == null)
            throw new InvalidOperationException("Actor is not set for this component.");

        return Actor.Transform.WorldMatrix;
    }
}
