using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Mesh;

public struct LayerMapping
{
    public uint ChannelIndex;
    public uint TextureIndex;
    public Vector4 DebugColor;
}

public unsafe struct PerMaterialLandscapeData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public uint WeightmapCount;

    public ulong Heightmap;
    public fixed ulong Weightmaps[4];
    public fixed uint Weight_EnabledChannels[4]; // packed data representing which channels are used in each weightmap texture (4 channels as 4 bytes in a uint)

    public Vector2 HeightmapScaleBias;
    public Vector2 WeightmapScaleBias;

    // Visibility (hole) layer — uint.MaxValue means no visibility layer on this tile
    public uint VisibilityTextureIndex;
    public uint VisibilityChannelIndex;
}

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : PrimitiveComponent<Vector2, PerMaterialLandscapeData>
{
    public readonly uint SizeQuads;
    public readonly Vector2[] Scales;
    public readonly Dictionary<string, LayerMapping> Layers;

    internal bool IsInitialized { get; set; } // TODO: rework this hack

    protected override bool SupportsOpaquePass => true;

    public LandscapeMeshComponent(ULandscapeComponent component) : base(component)
    {
        if (component.GetHeightmap() is not { } heightmap)
            throw new InvalidOperationException("Landscape component does not have a valid heightmap.");

        // Build CullingBounds in our geometry's local space, not from CachedLocalBox.
        //
        // Our vertex shader outputs geometry as a flat plane at Y=0, spanning
        // [0, sizeQuads*GlobalScale] in X and Z. The tessellation shader then displaces
        // each vertex along Y by: ((R*256+G) - 32768) / 128 * GlobalScale.
        //
        // CachedLocalBox.Min.Z and Max.Z store exactly that min/max height for this tile
        // in UE units (before GlobalScale), so we can use them directly for Y extents.
        //
        // Each tile gets its own descriptor (not cached) so bounds are per-tile.
        var sizeQuads = (uint)component.ComponentSizeQuads;
        var halfSize = sizeQuads * Settings.GlobalScale * 0.5f;
        var minHeight = component.CachedLocalBox.Min.Z * Settings.GlobalScale;
        var maxHeight = component.CachedLocalBox.Max.Z * Settings.GlobalScale;
        var tessellatedBounds = new CullingBounds(
            new Vector3(halfSize, (minHeight + maxHeight) * 0.5f, halfSize),
            new Vector3(halfSize, (maxHeight - minHeight) * 0.5f, halfSize));

        Descriptor = new PrimitiveDescriptor<Vector2>(tessellatedBounds, () => new Geometry(sizeQuads));

        SizeQuads = sizeQuads + 1;
        Scales = new Vector2[Settings.TessellationQuadCountTotal];

        const int quadCount = Settings.TessellationQuadCount;
        for (var x = 0; x < quadCount; x++)
        {
            for (var y = 0; y < quadCount; y++)
            {
                Scales[x * quadCount + y] = new Vector2(x, y);
            }
        }

        var textures = component.GetWeightmapTextures();
        var weightmaps = new Texture[textures.Length];
        for (var i = 0; i < weightmaps.Length; i++)
        {
            weightmaps[i] = new Texture2D(textures[i]);
        }

        Layers = new Dictionary<string, LayerMapping>();
        foreach (var allocation in component.WeightmapLayerAllocations)
        {
            if (!allocation.LayerInfo.TryLoad(out ULandscapeLayerInfoObject info)) continue;

            Layers.Add(info.LayerName.Text, new LayerMapping
            {
                ChannelIndex = allocation.WeightmapTextureChannel,
                TextureIndex = allocation.WeightmapTextureIndex,
                DebugColor = info.LayerUsageDebugColor
            });
        }

        Materials[0].InlineContainer = new MaterialDataContainer(
            new Texture2D(heightmap),
            new Vector2(component.HeightmapScaleBias.Z, component.HeightmapScaleBias.W),
            weightmaps,
            new Vector2(component.WeightmapScaleBias.Z, component.WeightmapScaleBias.W),
            component.WeightmapLayerAllocations,
            Layers.TryGetValue("__LANDSCAPE_VISIBILITY__", out var visibility) ? visibility : null);
    }

    internal override string Icon => "\uf6fc";

    private class MaterialDataContainer(
        Texture heightmap, Vector2 heightmapScaleBias,
        Texture[] weightmaps, Vector2 weightmapScaleBias,
        FWeightmapLayerAllocationInfo[] allocations,
        LayerMapping? visibilityMapping) : IMaterialDataContainer
    {
        private BindlessTexture? _heightmap;
        private BindlessTexture?[]? _weightmaps = new BindlessTexture[weightmaps.Length];

        public string Name => Settings.NoName;
        public bool HasTextures => true;
        public bool IsTranslucent => false;

        public Dictionary<string, Texture> GetTextures()
        {
            var textures = new Dictionary<string, Texture>
            {
                { "Heightmap", heightmap }
            };

            for (var i = 0; i < weightmaps.Length; i++)
            {
                textures[$"Weightmap_{i}"] = weightmaps[i];
            }

            return textures;
        }

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            var parts = key.Split('_');
            switch (parts[0])
            {
                case "Heightmap":
                    _heightmap = bindless;
                    break;
                case "Weightmap" when _weightmaps is not null && parts.Length == 2 && int.TryParse(parts[1], out var index):
                    _weightmaps[index] = bindless;
                    break;
                default:
                    throw new ArgumentException($"Unknown texture key: {key}");
            }
        }

        public void FinalizeGpuData()
        {
            if (Raw is not null)
                throw new InvalidOperationException("GPU data has already been finalized and sent.");

            if (_heightmap is null || _weightmaps?.Length != weightmaps.Length)
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");

            var data = new PerMaterialLandscapeData
            {
                IsReady = true,

                Heightmap = _heightmap,
                HeightmapScaleBias = heightmapScaleBias,

                WeightmapCount = (uint)weightmaps.Length,
                WeightmapScaleBias = weightmapScaleBias,

                VisibilityTextureIndex = visibilityMapping?.TextureIndex ?? uint.MaxValue,
                VisibilityChannelIndex = visibilityMapping?.ChannelIndex ?? 0u,
            };

            unsafe
            {
                for (var i = 0; i < 4; i++)
                {
                    if (i >= _weightmaps.Length) break;

                    var weightmap = _weightmaps[i];
                    if (weightmap is null)
                        throw new InvalidOperationException($"Weightmap at index {i} is not set.");

                    data.Weightmaps[i] = weightmap;
                    data.Weight_EnabledChannels[i] = 0;

                    foreach (var allocation in allocations)
                    {
                        if (allocation.WeightmapTextureIndex != i) continue;
                        data.Weight_EnabledChannels[i] |= 1u << allocation.WeightmapTextureChannel;
                    }
                }
            }

            Raw = data;
        }

        public IPerMaterialData? Raw { get; private set; }

        private int _selectedWeightmap;
        public void DrawControls()
        {
            EditorUI.Property("Heightmap Texture");
            if (_heightmap != null)
            {
                _heightmap.DrawControls();
            }
            else ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "None");

            EditorUI.Property($"Weightmaps ({weightmaps.Length})");

            var maxWeightmap = weightmaps.Length - 1;

            ImGui.BeginDisabled(maxWeightmap == 0);
            ImGui.SliderInt("##WeightmapSlider", ref _selectedWeightmap, 0, maxWeightmap);
            ImGui.EndDisabled();

            EditorUI.Property("Weightmap Texture");
            if (_weightmaps?[_selectedWeightmap] is { } weightmap)
            {
                weightmap.DrawControls();
            }
            else ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "None");
        }
    }

    protected override DebugComponent CreateDebugVisualization() => new BoxComponent(Descriptor.Bounds, Settings.LandscapeBounds, name: $"{Name} (Bounds)");

    private class Geometry : PrimitiveData<Vector2>
    {
        public Geometry(uint sizeQuads)
        {
            const int quadCount = Settings.TessellationQuadCount;
            var halfSize = sizeQuads / (float)quadCount;

            Vertices = new Vector2[Settings.TessellationIndicesPerQuad];
            Indices = new uint[Vertices.Length];

            for (var i = 0; i < quadCount; i++)
            {
                for (var j = 0; j < quadCount; j++)
                {
                    var index = i * quadCount + j;
                    var xOffset = i * halfSize;
                    var yOffset = j * halfSize;

                    Vertices[index * 4] = new Vector2(xOffset, yOffset);
                    Vertices[index * 4 + 1] = new Vector2(xOffset + halfSize, yOffset);
                    Vertices[index * 4 + 2] = new Vector2(xOffset, yOffset + halfSize);
                    Vertices[index * 4 + 3] = new Vector2(xOffset + halfSize, yOffset + halfSize);

                    Indices[index * 4] = (uint)(index * 4);
                    Indices[index * 4 + 1] = (uint)(index * 4 + 1);
                    Indices[index * 4 + 2] = (uint)(index * 4 + 2);
                    Indices[index * 4 + 3] = (uint)(index * 4 + 3);
                }
            }
        }
    }
}
