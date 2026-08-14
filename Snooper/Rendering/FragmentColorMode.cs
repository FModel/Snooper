namespace Snooper.Rendering;

public static class FragmentColorMode
{
    public const uint Disabled = 0;
    public const uint Clay = 1;
    public const uint ComponentId = 2;
    public const uint InstanceId = 3;
    public const uint DrawId = 4;
    public const uint VertexColor = 5;
    public const uint Normals = 6;
    public const uint BoneWeightPainting = 7;
    public const uint LODLevel = 8;
    public const uint MorphDisplacement = 9;

    public static readonly string[] Labels =
    [
        "Textures",
        "Clay",
        "Components",
        "Instances",
        "Draws",
        "Vertex Colors",
        "Normals",
        "Weight Painting",
        "LOD Level",
        "Morph Displacement",
    ];
}
