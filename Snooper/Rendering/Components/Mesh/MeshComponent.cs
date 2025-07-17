using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public struct PerDrawMeshData : IPerDrawData
{
    public bool IsReady { get; init; }
    public long Diffuse { get; init; }
    public long Normal { get; init; }
}

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : TPrimitiveComponent<Vertex, PerInstanceData, PerDrawMeshData>
{
    public sealed override PrimitiveSection[] Sections { get; protected init; }

    protected MeshComponent(CBaseMeshLod lod, ResolvedObject?[] materials) : base(new Geometry(lod))
    {
        Sections = new PrimitiveSection[lod.Sections.Value.Length];
        for (var i = 0; i < Sections.Length; i++)
        {
            var s = lod.Sections.Value[i];
            Sections[i] = new PrimitiveSection(s.FirstIndex, s.NumFaces * 3)
            {
                DrawDataContainer = new DrawDataContainer(new ColorTexture(FColor.Gray), new ColorTexture(FColor.Gray))
            };
        }
    }

    protected abstract IVertexData GetPrimitive(int index);
    
    private class DrawDataContainer(Texture diffuse, Texture normal) : IDrawDataContainer
    {
        private long _diffuse;
        private long _normal;
        
        public Dictionary<string, Texture> GetTextures() => new()
        {
            ["Diffuse"] = diffuse,
            ["Normal"] = normal
        };

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            switch (key)
            {
                case "Diffuse":
                    _diffuse = bindless;
                    break;
                case "Normal":
                    _normal = bindless;
                    break;
                default:
                    throw new ArgumentException($"Unknown texture key: {key}");
            }
        }

        public void FinalizeGpuData()
        {
            Raw = new PerDrawMeshData
            {
                IsReady = true,
                Diffuse = _diffuse,
                Normal = _normal
            };
        }

        public IPerDrawData? Raw { get; private set; }
    }
    
    protected readonly struct Geometry : IVertexData
    {
        public Vertex[] Vertices { get; }
        public uint[] Indices { get; }
        
        public Geometry(CBaseMeshLod lod)
        {
            var vertices = lod switch
            {
                CStaticMeshLod staticLod => staticLod.Verts,
                CSkelMeshLod skelLod => skelLod.Verts,
                _ => throw new NotSupportedException($"Unsupported mesh type: {lod.GetType().Name}")
            };

            Vertices = new Vertex[vertices.Length];
            for (var i = 0; i < Vertices.Length; i++)
            {
                var vertex = vertices[i];
                var position = new Vector3(vertex.Position.X, vertex.Position.Z, vertex.Position.Y) * Settings.GlobalScale;
                var normal = new Vector3(vertex.Normal.X, vertex.Normal.Z, vertex.Normal.Y);
                var tangent = new Vector3(vertex.Tangent.X, vertex.Tangent.Z, vertex.Tangent.Y);
                var texCoord = new Vector2(vertex.UV.U, vertex.UV.V);

                Vertices[i] = new Vertex(position, normal, tangent, texCoord);
            }

            Indices = new uint[lod.Indices.Value.Length];
            for (var i = 0; i < Indices.Length; i++)
            {
                Indices[i] = (uint) lod.Indices.Value[i];
            }
        }
    }
}
