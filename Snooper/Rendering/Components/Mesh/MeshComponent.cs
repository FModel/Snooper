using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Dto;
using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

/// <summary>
/// Packed vertex layout — 20 bytes total<br/>
///     loc 0: uvec2  — pos.x|pos.y (half2), pos.z|0 (half2)         [offset  0, 8 bytes]<br/>
///     loc 1: uint   — normal  xyzw RGB10A2 SNorm                   [offset  8, 4 bytes]<br/>
///     loc 2: uint   — tangent xyz RGB10A2 SNorm, w = texLayer(0-3) [offset 12, 4 bytes]<br/>
///     loc 3: uint   — uv.x|uv.y (half2)                            [offset 16, 4 bytes]<br/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vertex(Vector3 position, Vector4 normal, Vector3 tangent, Vector2 texCoord, uint texLayer)
{
    public readonly uint PosXY = PackHalf2(position.X, position.Y);
    public readonly uint PosZW = PackHalf2(position.Z, 1f); // free float
    public readonly uint NormalPacked = PackRgb10A2Snorm(normal);
    public readonly uint TangentPacked = PackRgb10A2Snorm(tangent, texLayer);
    public readonly uint TexCoordPacked = PackHalf2(texCoord.X, texCoord.Y);

    private static uint PackHalf2(float x, float y)
    {
        uint hx = BitConverter.HalfToUInt16Bits((Half)x);
        uint hy = BitConverter.HalfToUInt16Bits((Half)y);
        return hx | (hy << 16);
    }

    private static uint PackRgb10A2Snorm(Vector4 v) => PackRgb10A2Snorm(v.X, v.Y, v.Z, Snorm10(v.W));
    private static uint PackRgb10A2Snorm(Vector3 v, uint texLayer) => PackRgb10A2Snorm(v.X, v.Y, v.Z, texLayer & 0x3u);
    private static uint PackRgb10A2Snorm(float x, float y, float z, uint w) => Snorm10(x) | (Snorm10(y) << 10) | (Snorm10(z) << 20) | (w << 30);
    private static uint Snorm10(float f) => (uint)(int)MathF.Round(Math.Clamp(f, -1f, 1f) * 511f) & 0x3FFu;
}

public unsafe struct PerMaterialMeshData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public uint LayerCount; // Number of UV layers (1-4)
    public uint GlobalFlags;

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

public abstract class MeshComponent : PrimitiveComponent<Vertex, PerInstanceData, PerMaterialMeshData>
{
    private readonly FPackageIndex?[] _materials = [];
    private readonly List<UBuildingTextureData?> _textureData = [];

    public sealed override MaterialSection[] Materials { get; }

    protected override bool SupportsOpaquePass => true;

    protected MeshComponent(MeshComponent other) : base(other)
    {
        Materials = other.Materials;
    }

    protected MeshComponent(FPackageIndex?[] materials, Transform? transform = null, string? name = null) : base(transform, name)
    {
        _materials = materials;

        Materials = new MaterialSection[_materials.Length];
    }

    protected MeshComponent(FPackageIndex?[] materials, UMeshComponent component) : base(component)
    {
        _materials = materials;

        var overrideMaterials = component.OverrideMaterials;
        for (var i = 0; i < overrideMaterials.Length; i++)
        {
            if (i >= _materials.Length) break;
            if (overrideMaterials[i] is { IsNull: false } overrideMaterial)
            {
                _materials[i] = overrideMaterial;
            }
        }

        if (_materials.Length == 0)
        {
            _materials = [new FPackageIndex()];
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

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        if (Actor?.ActorManager is not { } manager || string.IsNullOrEmpty(Descriptor.Path) || string.IsNullOrEmpty(Descriptor.Name))
        {
            // this is a mesh but we don't have enough info to export it, fallback to a component export and pray that C4P can find the mesh
            base.Export(session, ct);
            return;
        }

        try
        {
            session.Add(manager.FileProvider.LoadPackageObject(Descriptor.Path, Descriptor.Name));

            // TODO: conflict here
            // no material export in options means skip exporting materials referenced by actual meshes
            // but what about the materials from the component??? (OverrideMaterials or user settable <- not implemented yet)
            // ig we should let the exporter handle that by exporting the component? but still, user settable?
            if (session.Options.ExportMaterials)
            {
                foreach (var ptr in _materials)
                {
                    ct.ThrowIfCancellationRequested();
                    if (ptr?.TryLoad<UMaterialInterface>(out var material) == true)
                    {
                        session.Add(material);
                    }
                }
            }
        }
        catch
        {
            //
        }
    }

    protected override DebugComponent CreateDebugVisualization() => new BoxComponent(Descriptor.Bounds, IsVisible ? Settings.VisibleMeshBounds : Settings.HiddenMeshBounds, name: $"{Name} (Bounds)");

    protected class Geometry : PrimitiveData<Vertex>
    {
        public Geometry(MeshVertex[] vertices, uint[] indices, FColor[]? colors, FMeshUVFloat[]? extraUvs)
        {
            Vertices = new Vertex[vertices.Length];
            for (var i = 0; i < Vertices.Length; i++)
            {
                var vertex = vertices[i];
                var position = new Vector3(vertex.Position.X, vertex.Position.Z, vertex.Position.Y) * Settings.GlobalScale;
                var normal = new Vector4(vertex.Normal.X, vertex.Normal.Z, vertex.Normal.Y, vertex.Normal.W);
                var tangent = new Vector3(vertex.Tangent.X, vertex.Tangent.Z, vertex.Tangent.Y);
                var texCoord = new Vector2(vertex.Uv.U, vertex.Uv.V);
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

        public Geometry(SkinnedMeshVertex[] vertices, uint[] indices, FColor[]? colors, FMeshUVFloat[]? extraUvs)
        {
            Vertices = new Vertex[vertices.Length];
            BoneInfluenceCounts = new byte[Vertices.Length];
            var influences = new List<uint>(BoneInfluenceCounts.Length * 4);

            for (var i = 0; i < Vertices.Length; i++)
            {
                var vertex = vertices[i];
                var position = new Vector3(vertex.Position.X, vertex.Position.Z, vertex.Position.Y) * Settings.GlobalScale;
                var normal = new Vector4(vertex.Normal.X, vertex.Normal.Z, vertex.Normal.Y, vertex.Normal.W);
                var tangent = new Vector3(vertex.Tangent.X, vertex.Tangent.Z, vertex.Tangent.Y);
                var texCoord = new Vector2(vertex.Uv.U, vertex.Uv.V);
                var texLayer = extraUvs != null ? (uint)Math.Floor(extraUvs[i].U) : 0u;

                Vertices[i] = new Vertex(position, normal, tangent, texCoord, texLayer);

                byte count = 0;
                foreach (var influence in vertex.Influences)
                {
                    if (influence.RawWeight == 0) continue;
                    influences.Add(((uint)influence.Bone << 16) | influence.RawWeight);
                    count++;
                }
                BoneInfluenceCounts[i] = count;
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

            BoneInfluences = influences.ToArray();
        }

        public Geometry(USkeleton skeleton)
        {
            var refSkeleton = skeleton.ReferenceSkeleton;
            var boneCount = refSkeleton.FinalRefBonePose.Length;
            Vertices = new Vertex[boneCount];
            var indices = new List<uint>();

            for (int i = 0; i < boneCount; i++)
            {
                var transform = refSkeleton.FinalRefBonePose[i];
                var position = new Vector3(transform.Translation.X, transform.Translation.Z, transform.Translation.Y) * Settings.GlobalScale;
                Vertices[i] = new Vertex(position, Vector4.UnitY, Vector3.UnitX, Vector2.Zero, 0);

                var parent = refSkeleton.FinalRefBoneInfo[i].ParentIndex;
                if (parent >= 0)
                {
                    indices.Add((uint)parent);
                    indices.Add((uint)i);
                }
            }
            Indices = indices.ToArray();
        }
    }
}
