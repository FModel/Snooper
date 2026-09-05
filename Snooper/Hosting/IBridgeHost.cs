using CUE4Parse_Conversion.Options;

namespace Snooper.Hosting;

public interface IBridgeHost
{
    public string Name { get; }
    public string ExportDirectory { get; }

    public bool OwnsLoadOptions => false;
    public bool CanBrowseAssets => false;

    public ExportOptions CreateExportOptions();
}

internal sealed class StandaloneHost : IBridgeHost
{
    public string Name => "Snooper";
    public string ExportDirectory => "./Exports";

    public ExportOptions CreateExportOptions() => new(
        naniteMeshFormat: Bridge.Options.NaniteMeshFormat,
        texturePlatform: Bridge.Options.TexturePlatform,
        materialDepth: Bridge.Options.MaterialDepth,
        exportMorphTargets: Bridge.Options.LoadMorphTargets);
}
