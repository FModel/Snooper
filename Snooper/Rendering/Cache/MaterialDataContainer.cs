using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Material;
using ImGuiNET;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.Rendering.Components.Mesh;
using Snooper.UI;

namespace Snooper.Rendering.Cache;

public enum MaterialTextureSlot
{
    Diffuse,
    Normal,
    Specular
}

public sealed class MaterialLayer(Texture? diffuse, Texture? normal, Texture? specular, Vector2 roughness, Vector3 diffuseColor)
{
    public Texture? Diffuse { get; private set; } = diffuse;
    public Texture? Normal { get; private set; } = normal;
    public Texture? Specular { get; private set; } = specular;

    public Vector2 Roughness { get; internal set; } = roughness;
    public Vector3 DiffuseColor { get; internal set; } = diffuseColor;

    public Texture? this[MaterialTextureSlot slot]
    {
        get => slot switch
        {
            MaterialTextureSlot.Diffuse => Diffuse,
            MaterialTextureSlot.Normal => Normal,
            MaterialTextureSlot.Specular => Specular,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
        internal set
        {
            switch (slot)
            {
                case MaterialTextureSlot.Diffuse: Diffuse = value; break;
                case MaterialTextureSlot.Normal: Normal = value; break;
                case MaterialTextureSlot.Specular: Specular = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }

    public MaterialLayer Clone() => new(Diffuse, Normal, Specular, Roughness, DiffuseColor);
}

public sealed class MaterialDataContainer : IMaterialDataContainer
{
    private readonly MaterialLayer[] _layers;
    private readonly BindlessTexture?[] _diffuses;
    private readonly BindlessTexture?[] _normals;
    private readonly BindlessTexture?[] _speculars;

    public string Name { get; }
    public EBlendMode BlendMode { get; internal set; }

    public IReadOnlyList<MaterialLayer> Layers => _layers;
    public int LayerCount => _layers.Length;

    public bool HasTextures => true;
    public bool IsTranslucent => BlendMode is not EBlendMode.BLEND_Opaque;

    public IPerMaterialData? Raw { get; private set; }
    public bool IsGpuDataReady => Raw is PerMaterialMeshData { IsReady: true };

    internal MaterialDataContainer(string name, MaterialLayer[] layers, EBlendMode blendMode)
    {
        Name = name;
        BlendMode = blendMode;

        _layers = layers;
        _diffuses = new BindlessTexture?[layers.Length];
        _normals = new BindlessTexture?[layers.Length];
        _speculars = new BindlessTexture?[layers.Length];
    }

    private MaterialDataContainer(MaterialDataContainer other)
    {
        Name = other.Name;
        BlendMode = other.BlendMode;

        _layers = new MaterialLayer[other._layers.Length];
        for (var i = 0; i < _layers.Length; i++)
        {
            _layers[i] = other._layers[i].Clone();
        }

        _diffuses = (BindlessTexture?[]) other._diffuses.Clone();
        _normals = (BindlessTexture?[]) other._normals.Clone();
        _speculars = (BindlessTexture?[]) other._speculars.Clone();
    }

    public MaterialDataContainer Clone()
    {
        var clone = new MaterialDataContainer(this);
        clone.FinalizeGpuData();
        return clone;
    }

    public Dictionary<string, Texture> GetTextures()
    {
        var dict = new Dictionary<string, Texture>();

        for (var i = 0; i < _layers.Length; i++)
        {
            if (_layers[i].Diffuse is { } diffuse) dict[$"Diffuse_{i}"] = diffuse;
            if (_layers[i].Normal is { } normal) dict[$"Normal_{i}"] = normal;
            if (_layers[i].Specular is { } specular) dict[$"Specular_{i}"] = specular;
        }

        return dict;
    }

    public void SetBindlessTexture(string key, BindlessTexture bindless)
    {
        var separator = key.LastIndexOf('_');
        if (separator < 0 || !int.TryParse(key[(separator + 1)..], out var index) || !Enum.TryParse<MaterialTextureSlot>(key[..separator], out var slot))
            throw new ArgumentException($"Unknown texture key: {key}");

        GetBindlessArray(slot)[index] = bindless;
    }

    public void SetLayerTexture(int layer, MaterialTextureSlot slot, Texture? texture, BindlessTexture? bindless)
    {
        if (layer < 0 || layer >= _layers.Length)
            throw new ArgumentOutOfRangeException(nameof(layer));

        _layers[layer][slot] = texture;
        GetBindlessArray(slot)[layer] = texture is null ? null : bindless;
    }

    public Texture? GetSlotTexture(int layer, MaterialTextureSlot slot) => layer >= 0 && layer < _layers.Length ? GetBindlessArray(slot)[layer]?.Texture : null;

    private BindlessTexture?[] GetBindlessArray(MaterialTextureSlot slot) => slot switch
    {
        MaterialTextureSlot.Diffuse => _diffuses,
        MaterialTextureSlot.Normal => _normals,
        MaterialTextureSlot.Specular => _speculars,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public void FinalizeGpuData()
    {
        // if (Raw is not null)
        //     throw new InvalidOperationException("GPU data has already been finalized and sent.");

        // each layer uses 3 bits: HasDiffuse (bit 0), HasNormal (bit 1), HasSpecular (bit 2)
        uint layerTextureFlags = 0;
        for (var i = 0; i < _layers.Length; i++)
        {
            uint layerFlags = 0;
            if (_diffuses[i] != null) layerFlags |= 1u; // HasDiffuse
            if (_normals[i] != null) layerFlags |= 2u;  // HasNormal
            if (_speculars[i] != null) layerFlags |= 4u; // HasSpecular

            layerTextureFlags |= layerFlags << (i * 3);
        }

        uint globalFlags = (uint) BlendMode & 0xF;

        var data = new PerMaterialMeshData
        {
            IsReady = true,
            LayerCount = (uint) _layers.Length,
            GlobalFlags = globalFlags,
            LayerTextureFlags = layerTextureFlags
        };

        unsafe
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                data.Diffuse[i] = _diffuses[i] ?? 0UL;
                data.Normal[i] = _normals[i] ?? 0UL;
                data.Specular[i] = _speculars[i] ?? 0UL;

                data.Roughness[i * 2] = _layers[i].Roughness.X;
                data.Roughness[i * 2 + 1] = _layers[i].Roughness.Y;

                data.DiffuseColor[i * 3] = _layers[i].DiffuseColor.X;
                data.DiffuseColor[i * 3 + 1] = _layers[i].DiffuseColor.Y;
                data.DiffuseColor[i * 3 + 2] = _layers[i].DiffuseColor.Z;
            }
        }

        Raw = data;
    }

    public void DrawControls()
    {
        EditorUI.Property("Material");
        DrawSummary();
    }

    public void DrawSummary(int layerIndex = 0)
    {
        layerIndex = Math.Clamp(layerIndex, 0, _layers.Length - 1);
        var thumbnailSize = ImGui.GetFrameHeight() * 2.0f;

        ImGui.BeginGroup();
        ImGui.TextUnformatted(Name);

        EditorUI.Caption($"Layer {layerIndex + 1}/{_layers.Length}, {BlendMode.GetDescription()}");

        DrawThumbnail(GetSlotTexture(layerIndex, MaterialTextureSlot.Diffuse), "D", thumbnailSize);
        ImGui.SameLine();
        DrawThumbnail(GetSlotTexture(layerIndex, MaterialTextureSlot.Normal), "N", thumbnailSize);
        ImGui.SameLine();
        DrawThumbnail(GetSlotTexture(layerIndex, MaterialTextureSlot.Specular), "S", thumbnailSize);

        if (!IsGpuDataReady)
        {
            ImGui.TextColored(Settings.OrangeColor, "Uploading...");
        }

        ImGui.EndGroup();
    }

    public static void DrawThumbnail(Texture? texture, string slotLabel, float size = 48.0f)
    {
        var dimensions = new Vector2(size);
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton($"##Thumb_{slotLabel}", dimensions);
        var drawList = ImGui.GetWindowDrawList();

        if (texture is null)
        {
            var labelSize = ImGui.CalcTextSize(slotLabel);
            drawList.AddText(origin + (dimensions - labelSize) * 0.5f, ImGui.GetColorU32(ImGuiCol.TextDisabled), slotLabel);
        }
        else
        {
            drawList.AddImage(texture.GetPointer(), origin, origin + dimensions);
        }

        drawList.AddRect(origin, origin + dimensions, ImGui.GetColorU32(ImGuiCol.Border));

        if (ImGui.IsItemHovered())
        {
            EditorUI.Tooltip(texture is null
                ? $"{slotLabel}: none"
                : $"{slotLabel}: {texture.Name}\n{texture.Width}x{texture.Height}, {texture.FormatName}, {texture.GetFormattedSpace()}");
        }

        if (clicked && texture is not null)
        {
            WindowRequests.Request(Settings.TextureInspectorWindow, texture);
        }
    }
}
