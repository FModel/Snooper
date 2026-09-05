using CUE4Parse_Conversion.Options;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;

namespace Snooper.Hosting;

public sealed class SnooperOptions
{
    public ENaniteMeshFormat NaniteMeshFormat { get; set; } = ENaniteMeshFormat.NoNanite;
    public EMaterialDepth MaterialDepth { get; set; } = EMaterialDepth.TopLayerOnly;
    public ETexturePlatform TexturePlatform { get; set; } = ETexturePlatform.DesktopMobile;
    public bool LoadMorphTargets { get; set; } = true;
    public int MaxTextureMipSize { get; set; } = 1024;
}
