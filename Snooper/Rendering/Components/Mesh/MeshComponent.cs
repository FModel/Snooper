using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.UObject;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Extensions;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Mesh;

public readonly struct Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord, uint texLayer)
{
    public readonly Vector3 Position = position;
    public readonly Vector3 Normal = normal;
    public readonly Vector3 Tangent = tangent;
    public readonly Vector2 TexCoord = texCoord;
    public readonly uint TexLayer = texLayer;
}

public unsafe struct PerMaterialMeshData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public uint LayerCount; // Number of UV layers (1-4)
    public uint GlobalFlags; // Bit 0: IsTranslucent, other bits available for global settings

    // Per-layer texture flags (3 bits per layer: HasDiffuse, HasNormal, HasSpecular)
    // Layer 0: bits 0-2, Layer 1: bits 3-5, Layer 2: bits 6-8, Layer 3: bits 9-11
    public uint LayerTextureFlags;

    // Fixed arrays for each layer (up to 4 layers)
    public fixed ulong Diffuse[4];
    public fixed ulong Normal[4];
    public fixed ulong Specular[4];

    // Per-layer material properties
    public fixed float Roughness[8]; // 2 floats per layer (min, max) * 4 layers
    public fixed float DiffuseColor[12]; // 3 floats per layer (RGB) * 4 layers
}

[DefaultActorSystem(typeof(RenderSystem))]
[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : PrimitiveComponent<Vertex, PerInstanceData, PerMaterialMeshData>
{
    private readonly ResolvedObject?[] _materials;

    public sealed override MaterialSection[] Materials { get; }

    protected MeshComponent(ResolvedObject?[] materials, Transform? transform = null, string? name = null) : base(transform, name)
    {
        _materials = materials;

        Materials = new MaterialSection[_materials.Length];
        // TODO: preload materials for basic properties (blend mode, etc.)
    }

    protected MeshComponent(ResolvedObject?[] materials, UMeshComponent component) : base(component)
    {
        _materials = materials;

        var overrideMaterials = component.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []);
        for (var i = 0; i < overrideMaterials.Length; i++)
        {
            if (i >= _materials.Length) break;
            if (overrideMaterials[i].IsNull) continue;

            _materials[i] = overrideMaterials[i].ResolvedObject;
        }

        if (_materials.Length == 0) // TODO: remove MaterialSection dependency when resources are being sent to the GPU
        {
            _materials = [new FPackageIndex().ResolvedObject];
        }

        Materials = new MaterialSection[_materials.Length];
        // TODO: preload materials for basic properties (blend mode, etc.)
    }

    protected override void OnActorAttachedToScene(IGameSystem scene)
    {
        base.OnActorAttachedToScene(scene);

        for (var i = 0; i < _materials.Length; i++)
        {
            var index = i;
            Materials[index] = new MaterialSection();

            if (Actor?.ActorManager == null)
                throw new InvalidOperationException("Actor or ActorManager is null when loading materials???");

            Actor?.ActorManager?.ThreadManager.Enqueue(() =>
            {
                if (_materials[index]?.TryLoad(out var m) == true && m is UUnrealMaterial material)
                {
                    var parameters = new CMaterialParams2();
                    material.GetParams(parameters, EMaterialFormat.FirstLayer);

                    Materials[index].Name = material.Name;
                    Materials[index].MaterialDataContainer = ParseMaterialParameters(parameters, material.Owner.Provider.ProjectName.ToUpperInvariant());
                }
                else
                {
                    Log.Warning("Material at index {MatIndex} is not valid or could not be loaded.", index);
                }
            });
        }
    }

    private MaterialDataContainer? ParseMaterialParameters(CMaterialParams2 parameters, string projectName)
    {
        // whatever we will probably remove this Switch thing later
        var maxLayers = Math.Min(4, Descriptor.Lods[0].LayerCount);
        if (parameters.Switches.TryGetValue("Use 2 Materials", out var value1) && value1)
            maxLayers = 2;
        if (parameters.Switches.TryGetValue("Use 3 Materials", out var value2) && value2)
            maxLayers = 3;
        if (parameters.Switches.TryGetValue("Use 4 Materials", out var value3) && value3)
            maxLayers = 4;

        var layers = new List<MaterialLayerData>();
        for (var layerIndex = 0; layerIndex < maxLayers && layerIndex < CMaterialParams2.Diffuse.Length; layerIndex++)
        {
            if (!parameters.TryGetTexture2d(out var diffuse, CMaterialParams2.Diffuse[layerIndex]))
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
            if (parameters.TryGetLinearColor(out var color, CMaterialParams2.DiffuseColors[layerIndex]))
            {
                color = color.ToSRGB();
                diffuseColor = new Vector3(color.R, color.G, color.B);
            }

            // get normal map for this layer
            parameters.TryGetTexture2d(out var normal, [..CMaterialParams2.Normals[layerIndex], CMaterialParams2.FallbackNormals]);

            // get specular map for this layer
            parameters.TryGetTexture2d(out var specular, [..CMaterialParams2.SpecularMasks[layerIndex], CMaterialParams2.FallbackSpecularMasks]);

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
                    specularTex.SwizzleMask =
                    [
                        (int) PixelFormat.Red,
                        (int) PixelFormat.Blue,
                        (int) PixelFormat.Green,
                        (int) PixelFormat.Alpha
                    ];
                }
                else
                {
                    specularTex.SwizzlePerGame(projectName);
                }
            }

            layers.Add(new MaterialLayerData(new Texture2D(diffuse), normal != null ? new Texture2D(normal) : null, specularTex, roughness, diffuseColor));
        }

        return layers.Count == 0 ? null : new MaterialDataContainer(layers.ToArray(), parameters.BlendMode is EBlendMode.BLEND_Translucent or EBlendMode.BLEND_Masked);
    }

    private readonly struct MaterialLayerData(Texture2D diffuse, Texture2D? normal, Texture2D? specular, Vector2 roughness, Vector3 diffuseColor)
    {
        public readonly Texture2D Diffuse = diffuse;
        public readonly Texture2D? Normal = normal;
        public readonly Texture2D? Specular = specular;
        public readonly Vector2 Roughness = roughness;
        public readonly Vector3 DiffuseColor = diffuseColor;
    }

    private class MaterialDataContainer(MaterialLayerData[] layers, bool translucent = false) : IMaterialDataContainer
    {
        private BindlessTexture?[]? _diffuses = new BindlessTexture?[layers.Length];
        private BindlessTexture?[]? _normals = new BindlessTexture?[layers.Length];
        private BindlessTexture?[]? _speculars = new BindlessTexture?[layers.Length];

        public bool HasTextures => true;
        public bool IsTranslucent { get; } = translucent;

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
            {
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");
            }

            for (var i = 0; i < layers.Length; i++)
            {
                _diffuses[i]?.Generate();
                _diffuses[i]?.MakeResident();

                _normals[i]?.Generate();
                _normals[i]?.MakeResident();

                _speculars[i]?.Generate();
                _speculars[i]?.MakeResident();
            }

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

            uint globalFlags = 0;
            if (IsTranslucent) globalFlags |= 1u; // Bit 0: IsTranslucent

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

            EditorUI.Property("Roughness");
            ImGui.TextUnformatted($"Min: {layer.Roughness.X:F2}, Max: {layer.Roughness.Y:F2}");

            EditorUI.Property("Translucent");
            ImGui.TextUnformatted(IsTranslucent ? "Yes" : "No");

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

        public void Dispose()
        {
            if (_diffuses is not null)
            {
                for (var i = 0; i < _diffuses.Length; i++)
                {
                    _diffuses[i]?.Dispose();
                }
                Array.Clear(_diffuses);
                _diffuses = null;
            }

            if (_normals is not null)
            {
                for (var i = 0; i < _normals.Length; i++)
                {
                    _normals[i]?.Dispose();
                }
                Array.Clear(_normals);
                _normals = null;
            }

            if (_speculars is not null)
            {
                for (var i = 0; i < _speculars.Length; i++)
                {
                    _speculars[i]?.Dispose();
                }
                Array.Clear(_speculars);
                _speculars = null;
            }

            Raw = null;
        }
    }

    protected class Geometry : PrimitiveData<Vertex>
    {
        public Geometry(CMeshVertex[] vertices, uint[] indices, FColor[]? colors, FMeshUVFloat[]? extraUvs)
        {
            Vertices = new Vertex[vertices.Length];
            for (var i = 0; i < Vertices.Length; i++)
            {
                var vertex = vertices[i];
                var position = new Vector3(vertex.Position.X, vertex.Position.Z, vertex.Position.Y) * Settings.GlobalScale;
                var normal = new Vector3(vertex.Normal.X, vertex.Normal.Z, vertex.Normal.Y);
                var tangent = new Vector3(vertex.Tangent.X, vertex.Tangent.Z, vertex.Tangent.Y);
                var texCoord = new Vector2(vertex.UV.U, vertex.UV.V);
                var texLayer = extraUvs != null ? (uint)Math.Floor(extraUvs[i].U) : 0u;

                Vertices[i] = new Vertex(position, normal, tangent, texCoord, texLayer);
            }

            Indices = indices;

            if (colors != null)
            {
                Colors = new int[colors.Length];
                for (var i = 0; i < Colors.Length; i++)
                {
                    Colors[i] = colors[i].ToPackedARGB();
                }
            }
        }
    }
}
