using CUE4Parse_Conversion.Options;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;

namespace Snooper.Hosting;

public sealed class SnooperOptions(
    ENaniteMeshFormat naniteMeshFormat = ENaniteMeshFormat.NoNanite,
    EMaterialDepth materialDepth = EMaterialDepth.TopLayerOnly,
    ETexturePlatform texturePlatform = ETexturePlatform.DesktopMobile,
    bool loadMorphTargets = true,
    ESocketFormat socketFormat = ESocketFormat.Bone,
    bool includeDisabledMeshSections = false,
    int maxTextureMipSize = 1024)
{
    public EMaterialDepth MaterialDepth { get; } = materialDepth;
    public int MaxTextureMipSize { get; } = maxTextureMipSize;

    private ExportOptions? _exportOptions;
    public ExportOptions ToExportOptions() => _exportOptions ??= new ExportOptions(
        naniteMeshFormat: naniteMeshFormat,
        meshQuality: EMeshQuality.All,
        texturePlatform: texturePlatform,
        // materialDepth: materialDepth,
        exportMaterials: true,
        exportMorphTargets: loadMorphTargets,
        socketFormat: socketFormat,
        includeDisabledMeshSections: includeDisabledMeshSections
    );
}
