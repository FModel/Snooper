namespace Snooper.Rendering.Components;

[Flags]
public enum DirtyFlags
{
    None = 0,
    Transform = 1 << 0,
    InstanceData = 1 << 1,
    Visibility = 1 << 2,
    ManualLodSwap = 1 << 3,
    Opacity = 1 << 4,
    Selection = 1 << 5,
    // X = 1 << Y,

    All = ~0
}

