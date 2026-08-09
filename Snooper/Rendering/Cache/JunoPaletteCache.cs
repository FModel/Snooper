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
        if (!provider.TryLoadPackageObject<UMaterialInterface>($"/JunoAtomAssets/Materials/{name}.{name}", out var material))
        {
            Log.Warning("Juno palette id {Id} has no {Name} material, falling back to gray", key, name);
            return _fallback;
        }

        if (!TryGetColor(material, out var color))
        {
            Log.Warning("Juno palette material {Name} has no Color parameter, falling back to gray", name);
            return _fallback;
        }

        return color.ToFColor(true);
    });

    private static bool TryGetColor(UUnrealMaterial? material, out FLinearColor color)
    {
        while (material is UMaterialInstanceConstant instance)
        {
            foreach (var parameter in instance.VectorParameterValues)
            {
                if (parameter is { Name: "Color", ParameterValue: { } value })
                {
                    color = value;
                    return true;
                }
            }

            var parent = instance.Parent;
            if (parent == material) break; // this should technically never happen
            material = parent;
        }

        color = default;
        return false;
    }

    public static void Clear() => _cache.Clear();
}
