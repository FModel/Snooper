using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CUE4Parse.UE4.Objects.UObject;

namespace Snooper.Rendering.Cache;

public static class MaterialCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(MaterialCache));

    private static readonly ConcurrentDictionary<string, Lazy<MaterialNode?>> _nodes = new();
    private static readonly ConcurrentDictionary<string, Lazy<(MaterialNode? Node, MaterialDataContainer? Container)>> _containers = new();

    public static string GetOrCreateKey(FPackageIndex? materialObject, uint layerCount)
    {
        if (!TryGetPath(materialObject, out var path)) return string.Empty;

        var newLazy = new Lazy<(MaterialNode?, MaterialDataContainer?)>(() =>
        {
            Log.Debug("Cache miss for material {Path}, creating data container", path);
            if (GetNode(materialObject) is not { } node)
            {
                Log.Warning("Material {Path} could not be loaded or is not valid.", path);
                return (null, null);
            }
            return (node, ParseMaterialParameters(node, layerCount, null));
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        _containers.GetOrAdd(path, newLazy);
        return path;
    }

    public static string GetOrCreateKeyFromTextureData(UBuildingTextureData?[] textureDataLayers, FPackageIndex? materialObject, uint layerCount)
    {
        if (!TryGetPath(materialObject, out var path)) return string.Empty;

        var dataHash = string.Join("|", textureDataLayers.Select(t => t?.GetPathName() ?? "null"));
        var key = $"__texdata__{path}__{dataHash}";

        var newLazy = new Lazy<(MaterialNode?, MaterialDataContainer?)>(() =>
        {
            Log.Debug("Cache miss for material {Path}, creating data container", path);

            MaterialNode? node = null;
            foreach (var textureData in textureDataLayers)
            {
                if (textureData is { OverrideMaterial.IsNull: false } && GetNode(textureData.OverrideMaterial) is { } overridden)
                {
                    node = overridden;
                    break;
                }
            }

            node ??= GetNode(materialObject);
            if (node == null)
            {
                Log.Warning("Building texture data has no override material and no base material");
                return (null, null);
            }

            return (node, ParseMaterialParameters(node, layerCount, textureDataLayers));
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        _containers.GetOrAdd(key, newLazy);
        return key;
    }

    public static MaterialNode? GetNode(FPackageIndex index)
    {
        return TryGetPath(index, out var path) ? GetNode(path, () => index.TryLoad<UUnrealMaterial>(out var material) ? material : null) : null;
    }

    public static MaterialNode? GetNode(string path, Func<UUnrealMaterial?> load)
    {
        var newLazy = new Lazy<MaterialNode?>(() => load() is { } material ? new MaterialNode(material) : null, LazyThreadSafetyMode.ExecutionAndPublication);
        return _nodes.GetOrAdd(path, newLazy).Value;
    }

    public static bool TryGetNode(string? key, [MaybeNullWhen(false)] out MaterialNode node)
    {
        node = !string.IsNullOrEmpty(key) && _containers.TryGetValue(key, out var lazy) && lazy.IsValueCreated ? lazy.Value.Node : null;
        return node is not null;
    }

    public static IMaterialDataContainer? Resolve(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return _containers.TryGetValue(key, out var lazy) ? lazy.Value.Container : null;
    }

    public static IEnumerable<(string Key, MaterialDataContainer Container)> GetLoaded()
    {
        foreach (var (key, lazy) in _containers)
        {
            if (lazy is { IsValueCreated: true, Value.Container: { } container })
                yield return (key, container);
        }
    }

    private static MaterialDataContainer? ParseMaterialParameters(MaterialNode node, uint layerCount, UBuildingTextureData?[]? textureDataLayers)
    {
        var maxLayers = Math.Min(4, layerCount);
        for (var count = 4u; count >= 2; count--)
        {
            if (!node.TryGetSwitch(out var enabled, $"Use {count} Materials") || !enabled) continue;
            maxLayers = count;
            break;
        }

        var layers = new List<MaterialLayer>();
        for (var layerIndex = 0; layerIndex < maxLayers && layerIndex < CMaterialParams2.Diffuse.Length; layerIndex++)
        {
            var layerTextureData = textureDataLayers != null && layerIndex < textureDataLayers.Length ? textureDataLayers[layerIndex] : null;

            var diffuse = layerTextureData?.Diffuse.Load<UTexture>();
            if (diffuse == null && !node.TryGetTexture(out diffuse, CMaterialParams2.Diffuse[layerIndex]) && layerIndex == 0)
                node.TryGetTexture(out diffuse, EMaterialTextureKind.Diffuse);

            var normal = layerTextureData?.Normal.Load<UTexture>();
            if (normal == null && !node.TryGetTexture(out normal, CMaterialParams2.Normals[layerIndex]) && layerIndex == 0)
                node.TryGetTexture(out normal, EMaterialTextureKind.Normal);

            var specular = layerTextureData?.Specular.Load<UTexture>();
            if (specular == null && !node.TryGetTexture(out specular, CMaterialParams2.SpecularMasks[layerIndex]) && layerIndex == 0)
                node.TryGetTexture(out specular, EMaterialTextureKind.SpecularMasks);

            var hasColor = false;
            var diffuseColor = Vector3.One;
            if (layerTextureData?.TintColor is { } tintColor)
            {
                hasColor = true;
                diffuseColor = new Vector3(tintColor.R / 255f, tintColor.G / 255f, tintColor.B / 255f);
            }
            else if (node.TryGetVector(out var color, CMaterialParams2.DiffuseColors[layerIndex]))
            {
                hasColor = true;
                color = color.ToSRGB();
                diffuseColor = new Vector3(color.R, color.G, color.B);
            }

            if (diffuse == null)
            {
                if (layerIndex > 0) continue;
                if (!hasColor && normal == null) return null;
            }

            var roughness = Vector2.UnitY;
            if (node.TryGetScalar(out var roughnessMin, "RoughnessMin", "SpecRoughnessMin"))
                roughness.X = roughnessMin;
            if (node.TryGetScalar(out var roughnessMax, "RoughnessMax", "SpecRoughnessMax"))
                roughness.Y = roughnessMax;

            Texture2D? specularTex = null;
            if (specular != null)
            {
                specularTex = new Texture2D(specular);
                if ((node.TryGetSwitch(out var srg, "SwizzleRoughnessToGreen") && srg) || node.HasTexture("SRM"))
                {
                    specularTex.SwizzleMask = [
                        (int)PixelFormat.Red,
                        (int)PixelFormat.Blue,
                        (int)PixelFormat.Green,
                        (int)PixelFormat.Alpha
                    ];
                }
                else
                {
                    specularTex.SwizzlePerGame(node.ProjectName.ToUpperInvariant());
                }
            }

            layers.Add(new MaterialLayer(diffuse != null ? new Texture2D(diffuse) : null, normal != null ? new Texture2D(normal) : null, specularTex, roughness, diffuseColor));
        }

        var materialName = textureDataLayers != null ? $"BuildingTexture_{node.Name}" : node.Name;
        return layers.Count == 0 ? null : new MaterialDataContainer(materialName, layers.ToArray(), node.BlendMode, node.ShadingModel);
    }

    private static bool TryGetPath([NotNullWhen(true)] FPackageIndex? index, [MaybeNullWhen(false)] out string path)
    {
        path = index?.ResolvedObject?.GetPathName();
        return !string.IsNullOrEmpty(path);
    }

    public static void ClearAndDispose()
    {
        Log.Information("Clearing material cache with {Count} containers and {Nodes} parsed materials", _containers.Count, _nodes.Count);
        _containers.Clear();
        _nodes.Clear();
        JunoPaletteCache.ClearAndDispose();
    }
}
