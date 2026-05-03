using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.UI;
using System.Collections.Concurrent;
using System.Numerics;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Cache;

public static class MaterialCache
{
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
        material.GetParams(parameters, EMaterialDepth.TopLayerOnly);

        // whatever we will probably remove this Switch thing later
        var maxLayers = Math.Min(4, layerCount);
        if (parameters.Switches.TryGetValue("Use 2 Materials", out var value1) && value1)
            maxLayers = 2;
        if (parameters.Switches.TryGetValue("Use 3 Materials", out var value2) && value2)
            maxLayers = 3;
        if (parameters.Switches.TryGetValue("Use 4 Materials", out var value3) && value3)
            maxLayers = 4;

        var layers = new List<MaterialLayerData>();
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

            layers.Add(new MaterialLayerData(new Texture2D(diffuse), normal != null ? new Texture2D(normal) : null, specularTex, roughness, diffuseColor));
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

    private readonly struct MaterialLayerData(Texture2D diffuse, Texture2D? normal, Texture2D? specular, Vector2 roughness, Vector3 diffuseColor)
    {
        public readonly Texture2D Diffuse = diffuse;
        public readonly Texture2D? Normal = normal;
        public readonly Texture2D? Specular = specular;
        public readonly Vector2 Roughness = roughness;
        public readonly Vector3 DiffuseColor = diffuseColor;
    }

    private class MaterialDataContainer(string name, MaterialLayerData[] layers, EBlendMode blendMode) : IMaterialDataContainer
    {
        private BindlessTexture?[]? _diffuses = new BindlessTexture?[layers.Length];
        private BindlessTexture?[]? _normals = new BindlessTexture?[layers.Length];
        private BindlessTexture?[]? _speculars = new BindlessTexture?[layers.Length];

        public string Name { get; } = name;
        public bool HasTextures => true;
        public bool IsTranslucent { get; } = blendMode is not EBlendMode.BLEND_Opaque;

        public Dictionary<string, Texture> GetTextures()
        {
            var dict = new Dictionary<string, Texture>();

            for (var i = 0; i < layers.Length; i++)
            {
                dict[$"Diffuse_{i}"] = layers[i].Diffuse;
                if (layers[i].Normal is { } normal) dict[$"Normal_{i}"] = normal;
                if (layers[i].Specular is { } specular) dict[$"Specular_{i}"] = specular;
            }

            return dict;
        }

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            var parts = key.Split('_');
            switch (parts[0])
            {
                case "Diffuse" when _diffuses is not null && parts.Length == 2 && int.TryParse(parts[1], out var index):
                    _diffuses[index] = bindless;
                    break;
                case "Normal" when _normals is not null && parts.Length == 2 && int.TryParse(parts[1], out var index):
                    _normals[index] = bindless;
                    break;
                case "Specular" when _speculars is not null && parts.Length == 2 && int.TryParse(parts[1], out var index):
                    _speculars[index] = bindless;
                    break;
                default:
                    throw new ArgumentException($"Unknown texture key: {key}");
            }
        }

        public void FinalizeGpuData()
        {
            if (Raw is not null)
                throw new InvalidOperationException("GPU data has already been finalized and sent.");

            if (_diffuses is null || _normals is null || _speculars is null)
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");

            // each layer uses 3 bits: HasDiffuse (bit 0), HasNormal (bit 1), HasSpecular (bit 2)
            uint layerTextureFlags = 0;
            for (var i = 0; i < layers.Length; i++)
            {
                uint layerFlags = 0;
                if (_diffuses[i] != null) layerFlags |= 1u; // HasDiffuse
                if (_normals[i] != null) layerFlags |= 2u;  // HasNormal
                if (_speculars[i] != null) layerFlags |= 4u; // HasSpecular

                layerTextureFlags |= layerFlags << (i * 3);
            }

            uint globalFlags = (uint)blendMode & 0xF;

            var data = new PerMaterialMeshData
            {
                IsReady = true,
                LayerCount = (uint)layers.Length,
                GlobalFlags = globalFlags,
                LayerTextureFlags = layerTextureFlags
            };

            unsafe
            {
                for (var i = 0; i < layers.Length; i++)
                {
                    data.Diffuse[i] = _diffuses[i] ?? 0UL;
                    data.Normal[i] = _normals[i] ?? 0UL;
                    data.Specular[i] = _speculars[i] ?? 0UL;

                    data.Roughness[i * 2] = layers[i].Roughness.X;
                    data.Roughness[i * 2 + 1] = layers[i].Roughness.Y;

                    data.DiffuseColor[i * 3] = layers[i].DiffuseColor.X;
                    data.DiffuseColor[i * 3 + 1] = layers[i].DiffuseColor.Y;
                    data.DiffuseColor[i * 3 + 2] = layers[i].DiffuseColor.Z;
                }
            }

            Raw = data;
        }

        public IPerMaterialData? Raw { get; private set; }

        private int _selectedLayer;
        public void DrawControls()
        {
            if (layers.Length == 0)
            {
                ImGui.TextDisabled("No layers available");
                return;
            }

            EditorUI.Property($"Layers ({layers.Length})");

            var maxLayer = layers.Length - 1;

            ImGui.BeginDisabled(maxLayer == 0);
            ImGui.SliderInt("##LayerSlider", ref _selectedLayer, 0, maxLayer);
            ImGui.EndDisabled();

            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
            ImGui.SetWindowFontScale(0.85f);

            var layer = layers[_selectedLayer];
            ImGui.TextUnformatted($"Diffuse{(layer.Normal != null ? " + Normal" : "")}{(layer.Specular != null ? " + Specular" : "")}");

            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopStyleVar();
            ImGui.Spacing();

            EditorUI.Property("Diffuse Texture");
            if (_diffuses?[_selectedLayer] is { } diffuse)
            {
                diffuse.DrawControls();
            }

            EditorUI.Property("Normal Texture");
            if (_normals?[_selectedLayer] is { } normal)
            {
                normal.DrawControls();
            }
            else ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "None");

            EditorUI.Property("Specular Texture");
            if (_speculars?[_selectedLayer] is { } specular)
            {
                specular.DrawControls();
            }
            else ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "None");

            EditorUI.Property("Diffuse Color");
            var diffuseColor = new Vector4(layer.DiffuseColor.X, layer.DiffuseColor.Y, layer.DiffuseColor.Z, 1.0f);
            ImGui.ColorButton("##DiffuseColor", diffuseColor, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoPicker, new Vector2(40, 20));
            ImGui.SameLine();
            ImGui.TextUnformatted($"RGB({layer.DiffuseColor.X:F2}, {layer.DiffuseColor.Y:F2}, {layer.DiffuseColor.Z:F2})");

            EditorUI.Text("Roughness", $"Min: {layer.Roughness.X:F2}, Max: {layer.Roughness.Y:F2}");
            EditorUI.Text("Blend Mode", ((byte) blendMode).ToString());
            EditorUI.Text("Translucent", IsTranslucent ? "\uf00c" : "\uf00d");

            EditorUI.Property("GPU Status");
            if (Raw is PerMaterialMeshData { IsReady: true } gpuData)
            {
                ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Ready");

                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                ImGui.SetWindowFontScale(0.85f);

                unsafe
                {
                    var layerFlags = (gpuData.LayerTextureFlags >> (_selectedLayer * 3)) & 7u;
                    var hasDiff = (layerFlags & 1u) != 0u;
                    var hasNorm = (layerFlags & 2u) != 0u;
                    var hasSpec = (layerFlags & 4u) != 0u;

                    ImGui.TextUnformatted($"Flags: {(hasDiff ? "D" : "-")}{(hasNorm ? "N" : "-")}{(hasSpec ? "S" : "-")}");
                    ImGui.TextUnformatted($"Handles: D={gpuData.Diffuse[_selectedLayer]:X}, N={gpuData.Normal[_selectedLayer]:X}, S={gpuData.Specular[_selectedLayer]:X}");
                }

                ImGui.SetWindowFontScale(1.0f);
                ImGui.PopStyleVar();
            }
            else
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Uploading...");
            }
        }
    }
}
