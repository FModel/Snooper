namespace Snooper.Rendering.Containers.Framebuffers;

public enum EDeferredTexture : byte
{
    Position = 0,
    Normal = 1,
    Color = 2,
    Specular = 3,
    Picking = 4
}

public enum EForwardTexture : byte
{
    Color = 0,
    Picking = 1
}

public enum EShadowTexture : byte
{
    Depth = 0
}

public enum EMaskTexture : byte
{
    Depth = 0
}

public enum EPostProcessTexture : byte
{
    Ao = 0,
    AoBlur = 1,
    Lit = 2,
    Combined = 3,
    PickingViz = 4,
    Aa = 5,
    ShadowViz = 6,
}
