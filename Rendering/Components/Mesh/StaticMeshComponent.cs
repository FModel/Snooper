using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public StaticMeshComponent(CStaticMesh staticMesh) : base(new Geometry(staticMesh.LODs[0]))
    {

    }

    public StaticMeshComponent(UStaticMesh staticMesh) : base(new Geometry(staticMesh.RenderData?.LODs[0]))
    {

    }

    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(CStaticMeshLod lod)
        {
            Vertices = lod.Verts.Select(x => new Vector3(x.Position.X, x.Position.Z, x.Position.Y) * Settings.GlobalScale).ToArray();

            Indices = new uint[lod.Indices.Value.Length];
            for (int i = 0; i < Indices.Length; i++)
            {
                Indices[i] = (uint) lod.Indices.Value[i];
            }
        }

        public Geometry(FStaticMeshLODResources lod)
        {
            if (lod.PositionVertexBuffer is not null)
            {
                Vertices = new Vector3[lod.PositionVertexBuffer.Verts.Length];
                for (int i = 0; i < Vertices.Length; i++)
                {
                    Vertices[i] = new Vector3(lod.PositionVertexBuffer.Verts[i].X, lod.PositionVertexBuffer.Verts[i].Z, lod.PositionVertexBuffer.Verts[i].Y) * Settings.GlobalScale;
                }
            }

            if (lod.IndexBuffer is not null)
            {
                Indices = new uint[lod.IndexBuffer.Length];
                for (int i = 0; i < Indices.Length; i++)
                {
                    Indices[i] = (uint) lod.IndexBuffer[i];
                }
            }
        }
    }
}
