using System.Numerics;
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
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

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
    private readonly List<UBuildingTextureData?> _textureData = [];

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

        for (var i = 0; i < _materials.Length; i++)
        {
            var index = i;
            var textureData = _textureData.ToArray();
            Materials[index] = new MaterialSection();

            if (Actor?.ActorManager == null)
                throw new InvalidOperationException("Actor or ActorManager is null when loading materials???");

            Actor?.ActorManager?.ThreadManager.Enqueue(() =>
            {
                if (index == 0 && textureData.Length > 0)
                {
                    Materials[index].MaterialDataContainer = MaterialCache.CreateFromTextureData(textureData, _materials[index], Descriptor.Lods[0].LayerCount);
                }
                else
                {
                    Materials[index].MaterialDataContainer = MaterialCache.GetOrCreate(_materials[index], Descriptor.Lods[0].LayerCount);
                }
            });
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
