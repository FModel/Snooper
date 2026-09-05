using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using System.Collections.Concurrent;
using System.Numerics;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Hosting;

namespace Snooper.Rendering.Cache;

public static class MaterialCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(MaterialCache));

    private static readonly ConcurrentDictionary<string, Lazy<IMaterialDataContainer?>> _cache = new();

    /// <summary>
    /// Resolves a previously registered cache key to its container, blocking until the container is ready.
    /// Returns null if the key is unknown or the container failed to load.
    /// </summary>
    public static IMaterialDataContainer? Resolve(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return _cache.TryGetValue(key, out var lazy) ? lazy.Value : null;
    }

    public static IEnumerable<(string Key, MaterialDataContainer Container)> GetLoaded()
    {
        foreach (var (key, lazy) in _cache)
        {
            if (lazy is { IsValueCreated: true, Value: MaterialDataContainer container })
                yield return (key, container);
        }
    }

    /// <summary>
    /// Returns the cache key and ensures a <see cref="Lazy{T}"/> entry exists for it,
    /// without blocking on the actual container creation.
    /// The container is created on first call to <see cref="Resolve"/>.
    /// </summary>
    public static string GetOrCreateKey(FPackageIndex? materialObject, uint layerCount)
    {
        if (materialObject == null) return string.Empty;

        var path = materialObject.ResolvedObject?.GetPathName();
        var newLazy = new Lazy<IMaterialDataContainer?>(() =>
        {
            Log.Debug("Cache miss for material {Path}, creating data container", path);
            if (!materialObject.TryLoad(out var m) || m is not UUnrealMaterial material)
            {
                Log.Warning("Material {Path} could not be loaded or is not valid.", path);
                return null;
            }
            return ParseMaterialParameters(material, layerCount, null);
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        _cache.GetOrAdd(path, newLazy);
        return path;
    }

    public static string GetOrCreateKeyFromTextureData(UBuildingTextureData?[] textureDataLayers, FPackageIndex? materialObject, uint layerCount)
    {
        if (materialObject == null) return string.Empty;

        var path = materialObject.ResolvedObject?.GetPathName();
        var dataHash = string.Join("|", textureDataLayers.Select(t => t?.GetPathName() ?? "null"));
        var key = $"__texdata__{path}__{dataHash}";

        var newLazy = new Lazy<IMaterialDataContainer?>(() =>
        {
            Log.Debug("Cache miss for material {Path}, creating data container", path);

            UUnrealMaterial? baseMaterial = null;
            foreach (var textureData in textureDataLayers)
            {
                if (textureData?.OverrideMaterial.TryLoad<UUnrealMaterial>(out var overrideMaterial) == true)
                {
                    baseMaterial = overrideMaterial;
                    break;
                }
            }

            if (baseMaterial == null && materialObject.TryLoad(out var m) && m is UUnrealMaterial material)
                baseMaterial = material;

            if (baseMaterial == null)
            {
                Log.Warning("Building texture data has no override material and no base material");
                return null;
            }

            return ParseMaterialParameters(baseMaterial, layerCount, textureDataLayers);
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        _cache.GetOrAdd(key, newLazy);
        return key;
    }

    private static MaterialDataContainer? ParseMaterialParameters(UUnrealMaterial material, uint layerCount, UBuildingTextureData?[]? textureDataLayers)
    {
        var parameters = new CMaterialParams2();
        material.GetParams(parameters, Bridge.Options.MaterialDepth);

        // whatever we will probably remove this Switch thing later
        var maxLayers = Math.Min(4, layerCount);
        if (parameters.Switches.TryGetValue("Use 2 Materials", out var value1) && value1)
            maxLayers = 2;
        if (parameters.Switches.TryGetValue("Use 3 Materials", out var value2) && value2)
            maxLayers = 3;
        if (parameters.Switches.TryGetValue("Use 4 Materials", out var value3) && value3)
            maxLayers = 4;

        var layers = new List<MaterialLayer>();
        for (var layerIndex = 0; layerIndex < maxLayers && layerIndex < CMaterialParams2.Diffuse.Length; layerIndex++)
        {
            var layerTextureData = textureDataLayers != null && layerIndex < textureDataLayers.Length ? textureDataLayers[layerIndex] : null;

            var diffuse = layerTextureData?.Diffuse.Load<UTexture>();
            if (diffuse == null && !parameters.TryGetTexture2d(out diffuse, CMaterialParams2.Diffuse[layerIndex]))
            {
                if (layerIndex == 0)
                {
                    if (!parameters.TryGetTexture2d(out diffuse, CMaterialParams2.FallbackDiffuse))
                    {
                        parameters.TryGetFirstTexture2d(out diffuse);
                    }
                }

                if (diffuse == null)
                {
                    // layer 0 has no diffuse, don't bother continuing
                    if (layerIndex == 0)
                        return null;

                    // no diffuse texture found for this layer, skip it
                    continue;
                }
            }

            var diffuseColor = Vector3.One;
            if (layerTextureData?.TintColor is { } tintColor)
            {
                diffuseColor = new Vector3(tintColor.R / 255f, tintColor.G / 255f, tintColor.B / 255f);
            }
            else if (parameters.TryGetLinearColor(out var color, CMaterialParams2.DiffuseColors[layerIndex]))
            {
                color = color.ToSRGB();
                diffuseColor = new Vector3(color.R, color.G, color.B);
            }

            var normal = layerTextureData?.Normal.Load<UTexture>();
            if (normal == null)
            {
                parameters.TryGetTexture2d(out normal, [..CMaterialParams2.Normals[layerIndex], CMaterialParams2.FallbackNormals]);
            }

            var specular = layerTextureData?.Specular.Load<UTexture>();
            if (specular == null)
            {
                parameters.TryGetTexture2d(out specular, [..CMaterialParams2.SpecularMasks[layerIndex], CMaterialParams2.FallbackSpecularMasks]);
            }

            var roughness = Vector2.UnitY;
            if (parameters.TryGetScalar(out var roughnessMin, "RoughnessMin", "SpecRoughnessMin"))
                roughness.X = roughnessMin;
            if (parameters.TryGetScalar(out var roughnessMax, "RoughnessMax", "SpecRoughnessMax"))
                roughness.Y = roughnessMax;

            Texture2D? specularTex = null;
            if (specular != null)
            {
                specularTex = new Texture2D(specular);
                if ((parameters.TryGetSwitch(out var srg, "SwizzleRoughnessToGreen") && srg) || parameters.Textures.ContainsKey("SRM"))
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
                    specularTex.SwizzlePerGame(material.Owner.Provider.ProjectName.ToUpperInvariant());
                }
            }

            layers.Add(new MaterialLayer(new Texture2D(diffuse), normal != null ? new Texture2D(normal) : null, specularTex, roughness, diffuseColor));
        }

        var materialName = textureDataLayers != null ? $"BuildingTexture_{material.Name}" : material.Name;
        return layers.Count == 0 ? null : new MaterialDataContainer(materialName, layers.ToArray(), parameters.BlendMode);
    }

    public static void ClearAndDispose()
    {
        foreach (var lazy in _cache.Values)
        {
            if (lazy is { IsValueCreated: true, Value: IDisposable disposable })
            {
                disposable.Dispose();
            }
        }

        Log.Information("Clearing material cache with {Count} entries", _cache.Count);
        _cache.Clear();
    }
}
