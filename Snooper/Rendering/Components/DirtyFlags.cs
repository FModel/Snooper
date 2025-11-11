namespace Snooper.Rendering.Components;

[Flags]
public enum DirtyFlags
{
    None = 0,
    InstanceData = 1 << 0,
    Visibility = 1 << 1,
    // X = 1 << 2,
    // X = 1 << 3,
    
    All = ~0
}

