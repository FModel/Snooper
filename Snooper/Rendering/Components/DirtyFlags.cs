namespace Snooper.Rendering.Components;

[Flags]
public enum DirtyFlags
{
    None = 0,
    Transform = 1 << 0,
    InstanceData = 1 << 1,
    Visibility = 1 << 2,
    // X = 1 << Y,
    
    All = ~0
}

