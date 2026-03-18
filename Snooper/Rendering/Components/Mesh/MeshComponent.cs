using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

/// <summary>
/// Packed vertex layout — 20 bytes total<br/>
///     loc 0: uvec2  — pos.x|pos.y (half2), pos.z|0 (half2)         [offset  0, 8 bytes]<br/>
///     loc 1: uint   — normal  xyz RGB10A2 SNorm, w = texLayer(0-3) [offset  8, 4 bytes]<br/>
///     loc 2: uint   — tangent xyz RGB10A2 SNorm, w = unused        [offset 12, 4 bytes]<br/>
///     loc 3: uint   — uv.x|uv.y (half2)                            [offset 16, 4 bytes]<br/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord, uint texLayer)
{
    public readonly uint PosXY = PackHalf2(position.X, position.Y);
    public readonly uint PosZW = PackHalf2(position.Z, 1f); // free float
    public readonly uint NormalPacked = PackRgb10A2Snorm(normal,  texLayer);
    public readonly uint TangentPacked = PackRgb10A2Snorm(tangent, 0u); // free uint
    public readonly uint TexCoordPacked = PackHalf2(texCoord.X, texCoord.Y);

    private static uint PackHalf2(float x, float y)
    {
        uint hx = BitConverter.HalfToUInt16Bits((Half)x);
        uint hy = BitConverter.HalfToUInt16Bits((Half)y);
        return hx | (hy << 16);
    }

    private static uint PackRgb10A2Snorm(Vector3 v, uint w)
    {
        return Snorm10(v.X) | (Snorm10(v.Y) << 10) | (Snorm10(v.Z) << 20) | ((w & 0x3u) << 30);

        uint Snorm10(float f)
        {
            var i = (int)MathF.Round(Math.Clamp(f, -1f, 1f) * 511f);
            return (uint)i & 0x3FFu;
        }
    }
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

[DefaultActorSystem(typeof(MeshRenderSystem))]
public abstract class MeshComponent : PrimitiveComponent<Vertex, PerInstanceData, PerMaterialMeshData>
{
    private readonly ResolvedObject?[] _materials;
    private readonly List<UBuildingTextureData?> _textureData = [];

    public sealed override MaterialSection[] Materials { get; }

    protected override bool SupportsOpaquePass => true;

    protected MeshComponent(ResolvedObject?[] materials, Transform? transform = null, string? name = null) : base(transform, name)
    {
        _materials = materials;

        Materials = new MaterialSection[_materials.Length];
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

        if (_materials.Length == 0)
        {
            _materials = [new FPackageIndex().ResolvedObject];
        }

        Materials = new MaterialSection[_materials.Length];
    }

    public void RegisterTextureData(UBuildingTextureData textureData, int layerIndex)
    {
        while (_textureData.Count <= layerIndex)
        {
            _textureData.Add(null);
        }
        _textureData[layerIndex] = textureData;
    }

    protected override void OnActorAttachedToScene(IGameSystem scene)
    {
        base.OnActorAttachedToScene(scene);

        for (var i = 0u; i < _materials.Length; i++)
        {
            var index = i;
            var textureData = _textureData.ToArray();
            Materials[index] = new MaterialSection(index);

            if (Actor?.ActorManager == null)
                throw new InvalidOperationException("Actor or ActorManager is null when loading materials???");

            Actor?.ActorManager?.ThreadManager.Enqueue(() =>
            {
                string key;
                if (index == 0 && textureData.Length > 0)
                {
                    key = MaterialCache.GetOrCreateKeyFromTextureData(textureData, _materials[index], Descriptor.Lods[0].LayerCount);
                }
                else
                {
                    key = MaterialCache.GetOrCreateKey(_materials[index], Descriptor.Lods[0].LayerCount);
                }

                Materials[index].CacheKey = key;
            });
        }
    }

    protected override DebugComponent CreateDebugVisualization() => new BoxComponent(Descriptor.Bounds, IsVisible ? Settings.VisibleMeshBounds : Settings.HiddenMeshBounds, name: $"{Name} (Bounds)");

    protected class Geometry : PrimitiveData<Vertex>
    {
        public Geometry(CMeshVertex[] vertices, uint[] indices, FColor[]? colors, FMeshUVFloat[]? extraUvs)
        {
            var counts = vertices is CSkelMeshVertex[] ? new byte[vertices.Length] : null;
            var influences = counts != null ? new List<uint>(counts.Length * 4) : null;

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

                if (vertex is CSkelMeshVertex skVertex && influences != null && counts != null)
                {
                    byte count = 0;
                    foreach (var influence in skVertex.Influences)
                    {
                        if (influence.RawWeight == 0) continue;
                        influences.Add(((uint)influence.Bone << 16) | influence.RawWeight);
                        count++;
                    }
                    counts[i] = count;
                }
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

            if (influences != null && counts != null)
            {
                BoneInfluences = influences.ToArray();
                BoneInfluenceCounts = counts;
            }
        }
    }
}
