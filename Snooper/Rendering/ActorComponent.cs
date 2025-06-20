namespace Snooper.Rendering;

public abstract class ActorComponent
{
    public Actor? Actor;
    public bool IsDirty = true;

    public bool IsVisible => Actor?.IsVisible ?? false;
}
