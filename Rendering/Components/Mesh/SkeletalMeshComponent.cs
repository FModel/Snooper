using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    private readonly USkeletalMesh _owner;

    public SkeletalMeshComponent(CSkeletalMesh skeletalMesh) : base(new Geometry(skeletalMesh.LODs.First()))
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh) : base(new Geometry(skeletalMesh.LODModels.First()))
    {
        _owner = skeletalMesh;
        var lodInfo = skeletalMesh.GetOrDefault<FStructFallback[]>("LODInfo", []);
        ScreenSizes = new float[lodInfo.Length];
        for (var i = 0; i < lodInfo.Length; i++)
        {
            ScreenSizes[i] = 1 - lodInfo[i].Get<TPerPlatformProperty.FPerPlatformFloat>("ScreenSize").Default;
        }
    }

    public override IPrimitiveData GetPrimitive(int index) => new Geometry(_owner.LODModels[LODIndex]);

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

        public Geometry(FStaticLODModel lod)
        {
            Vertices = new Vector3[lod.VertexBufferGPUSkin.GetVertexCount()];
            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i] = new Vector3(lod.VertexBufferGPUSkin.VertsFloat[i].Pos.X, lod.VertexBufferGPUSkin.VertsFloat[i].Pos.Z, lod.VertexBufferGPUSkin.VertsFloat[i].Pos.Y) * Settings.GlobalScale;
            }

            if (lod.Indices is not null)
            {
                Indices = new uint[lod.Indices.Indices16.Length];
                for (int i = 0; i < Indices.Length; i++)
                {
                    Indices[i] = (uint) lod.Indices.Indices16[i];
                }
            }
        }
    }
}
