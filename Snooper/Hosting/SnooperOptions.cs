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
    public bool IncludeDisabledMeshSections { get; set; }
    public int MaxTextureMipSize { get; set; } = 1024;

    // more like ToMeshOptions
    public ExportOptions ToExportOptions() => new(
        meshQuality: EMeshQuality.All,
        naniteMeshFormat: NaniteMeshFormat,
        // materialDepth: MaterialDepth,
        // texturePlatform: TexturePlatform,
        exportMorphTargets: LoadMorphTargets,
        includeDisabledMeshSections: IncludeDisabledMeshSections
    );
}
