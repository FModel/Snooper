namespace Snooper.Rendering.Components;

[Flags]
public enum DirtyFlags : uint
{
    None = 0,
    Transform = 1u << 0,
    InstanceData = 1u << 1,
    Visibility = 1u << 2,
    ManualLodSwap = 1u << 3,
    Opacity = 1u << 4,
    Outline = 1u << 5,
    Spline = 1u << 6,
    Animation = 1u << 7,
    Morph = 1u << 8,
    // X = 1u << Y,

    All = uint.MaxValue
}

