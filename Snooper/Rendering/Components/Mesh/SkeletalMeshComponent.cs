using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    public override int LODCount => _mesh.LODs.Count;
    public override CMeshSection[] Sections => _mesh.LODs[LODIndex].Sections.Value;

    private readonly CSkeletalMesh _mesh;

    public SkeletalMeshComponent(USkeletalMesh owner, CSkeletalMesh mesh) : base(new Geometry(mesh.LODs.First()))
    {
        _mesh = mesh;

        var lodInfo = owner.GetOrDefault<FStructFallback[]>("LODInfo", []);
        ScreenSizes = new float[lodInfo.Length];
        for (var i = 0; i < lodInfo.Length; i++)
        {
            ScreenSizes[i] = lodInfo[i].Get<TPerPlatformProperty.FPerPlatformFloat>("ScreenSize").Default;
        }
    }

    protected override IVertexData GetPrimitive(int index) => new Geometry(_mesh.LODs[index]);

    private readonly struct Geometry : IVertexData
    {
        public Vertex[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(CSkelMeshLod lod)
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
