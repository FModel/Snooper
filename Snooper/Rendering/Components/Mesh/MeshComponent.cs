using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Material;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public struct PerDrawMeshData : IPerDrawData
{
    public long DiffuseTexture { get; set; }
    public long NormalTexture { get; set; }
}

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : TPrimitiveComponent<Vertex, PerInstanceData, PerDrawMeshData>
{
    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; }

    protected MeshComponent(CBaseMeshLod lod, ResolvedObject?[] materials) : base(new Geometry(lod))
    {
        var length = lod.Sections.Value.Length;

        MaterialSections = new MeshMaterialSection[length];
        for (var i = 0; i < MaterialSections.Length; i++)
        {
            var s = lod.Sections.Value[i];
            
            MaterialSections[i] = new MeshMaterialSection(s.MaterialIndex, s.FirstIndex, s.NumFaces * 3);
            MaterialSections[i].ParseMaterialAsync(materials, () =>
            {
                if (Interlocked.Decrement(ref length) == 0)
                {
                    DrawDataDirty = true;
                }
            });
        }
    }

    protected override void ApplyDrawData(PerDrawMeshData[] data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            var section = MaterialSections[i];
            if (!section.Parameters.TryGetTexture2d(out var texture, CMaterialParams2.FallbackDiffuse) &&
                !section.Parameters.TryGetFirstTexture2d(out texture))
            {
                continue;
            }

            var bindless = new BindlessTexture(new Texture2D(texture));
            bindless.Generate();
            bindless.MakeResident();
            
            data[i].DiffuseTexture = bindless;
            data[i].NormalTexture = 0;
        }
    }

    protected abstract IVertexData GetPrimitive(int index);
    
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
