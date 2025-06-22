using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public override int LODCount => _mesh.LODs.Count;
    public override CMeshSection[] Sections => _mesh.LODs[LODIndex].Sections.Value;
    
    private readonly CStaticMesh _mesh;

    public StaticMeshComponent(UStaticMesh owner, CStaticMesh mesh) : base(new Geometry(mesh.LODs[0]))
    {
        _mesh = mesh;

        ScreenSizes = owner.RenderData?.ScreenSize ?? [];
    }

    protected override IVertexData GetPrimitive(int index) => new Geometry(_mesh.LODs[index]);

    private readonly struct Geometry : IVertexData
    {
        public Vertex[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(CStaticMeshLod lod)
        {
            Vertices = new Vertex[lod.Verts.Length];
            for (var i = 0; i < Vertices.Length; i++)
            {
                var vertex = lod.Verts[i];
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
