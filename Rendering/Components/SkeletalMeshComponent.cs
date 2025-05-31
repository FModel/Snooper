using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components;

public class SkeletalMeshComponent(CSkeletalMesh skeletalMesh) : MeshComponent(new Geometry(skeletalMesh.LODs[0]), skeletalMesh.BoundingBox)
{
    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(CSkelMeshLod lod)
        {
            Vertices = lod.Verts.Select(x => new Vector3(x.Position.X, x.Position.Z, x.Position.Y) * Settings.GlobalScale).ToArray();

            Indices = new uint[lod.Indices.Value.Length];
            for (int i = 0; i < Indices.Length; i++)
            {
                Indices[i] = (uint) lod.Indices.Value[i];
            }
        }
    }
}
