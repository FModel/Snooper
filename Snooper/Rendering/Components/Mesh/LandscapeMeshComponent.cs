using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

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
}

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : PrimitiveComponent<Vector2, PerMaterialLandscapeData>
{
    public readonly uint SizeQuads;
    public readonly Vector2[] Scales;
    public readonly Dictionary<string, LayerMapping> Layers;

    protected override bool SupportsOpaquePass => true;

    public LandscapeMeshComponent(ULandscapeComponent component) : base(component)
    {
        var sizeQuads = (uint)component.ComponentSizeQuads;
        // frustum culling is broken because the bounding box is for the cached geometry, not the actual tessellated one
        // do not cache the geometry to fix this
        Descriptor = PrimitiveDescriptor<Vector2>.GetOrCreate(sizeQuads, component.CachedLocalBox, id => new Geometry(id));

        if (component.GetHeightmap() is not { } heightmap)
        {
            throw new InvalidOperationException("Landscape component does not have a valid heightmap.");
        }

        var textures = component.GetWeightmapTextures();
        var weightmaps = new Texture[textures.Length];
        for (var i = 0; i < weightmaps.Length; i++)
        {
            weightmaps[i] = new Texture2D(textures[i]);
        }

        Materials[0].MaterialDataContainer = new MaterialDataContainer(
            new Texture2D(heightmap),
            new Vector2(component.HeightmapScaleBias.Z, component.HeightmapScaleBias.W),
            weightmaps,
            new Vector2(component.WeightmapScaleBias.Z, component.WeightmapScaleBias.W),
            component.WeightmapLayerAllocations);

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
    }

    public override string Icon => "\uf6fc";

    private class MaterialDataContainer(Texture heightmap, Vector2 heightmapScaleBias, Texture[] weightmaps, Vector2 weightmapScaleBias, FWeightmapLayerAllocationInfo[] allocations) : IMaterialDataContainer
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
            {
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");
            }

            _heightmap.Generate();
            _heightmap.MakeResident();

            var data = new PerMaterialLandscapeData
            {
                IsReady = true,

                Heightmap = _heightmap,
                HeightmapScaleBias = heightmapScaleBias,

                WeightmapCount = (uint)weightmaps.Length,
                WeightmapScaleBias = weightmapScaleBias,
            };

            unsafe
            {
                for (var i = 0; i < 4; i++)
                {
                    if (i >= _weightmaps.Length) break;

                    var weightmap = _weightmaps[i];
                    if (weightmap is null)
                    {
                        throw new InvalidOperationException($"Weightmap at index {i} is not set.");
                    }

                    weightmap.Generate();
                    weightmap.MakeResident();

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

        public void DrawControls()
        {

        }

        public void Dispose()
        {
            _heightmap?.Dispose();
            _heightmap = null;

            if (_weightmaps is not null)
            {
                for (var i = 0; i < _weightmaps.Length; i++)
                {
                    _weightmaps[i]?.Dispose();
                }
                Array.Clear(_weightmaps);
                _weightmaps = null;
            }

            Raw = null;
        }
    }

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
