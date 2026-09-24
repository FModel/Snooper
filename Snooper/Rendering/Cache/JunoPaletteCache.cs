using System.Collections.Concurrent;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Objects.Core.Math;
using Serilog;

namespace Snooper.Rendering.Cache;

public static class JunoPaletteCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(JunoPaletteCache));

    private static readonly ConcurrentDictionary<int, FColor> _cache = new();

    private static readonly FColor _fallback = FLinearColor.Gray.ToFColor(true);

    public static FColor Resolve(IFileProvider provider, FColor id) => _cache.GetOrAdd(id.R + id.G + id.B, key =>
    {
        var name = $"MI_LegoStandard_{key}";
        var path = $"/JunoAtomAssets/Materials/{name}.{name}";
        if (MaterialCache.GetNode(path, () => provider.TryLoadPackageObject<UMaterialInterface>(path, out var material) ? material : null) is not { } node)
        {
            Log.Warning("Juno palette id {Id} has no {Name} material, falling back to gray", key, name);
            return _fallback;
        }

        if (!node.TryGetVector(out var color, "Color"))
        {
            Log.Warning("Juno palette material {Name} has no Color parameter, falling back to gray", name);
            return _fallback;
        }

        return color.ToFColor(true);
    });

    public static void ClearAndDispose() => _cache.Clear();
}
