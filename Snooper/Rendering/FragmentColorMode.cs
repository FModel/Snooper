namespace Snooper.Rendering;

public static class FragmentColorMode
{
    public const int Disabled = 0;
    public const int Clay = 1;
    public const int ComponentId = 2;
    public const int InstanceId = 3;
    public const int DrawId = 4;
    public const int VertexColor = 5;
    public const int Normals = 6;
    public const int BoneWeightPainting = 7;
    // public const int LODLevel          = 9;
    // public const int LightInfluence    = 10;
    // public const int ShadowCascades    = 11;

    public static readonly string[] Labels =
    [
        "Disabled",
        "Clay",
        "Show Components",
        "Show Instances",
        "Show Draws",
        "Show Vertex Colors",
        "Show Normals",
        "Bone Weight Painting",
    ];
}
