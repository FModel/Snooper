using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components;

public class SkeletalMeshComponent : MeshComponent
{
    private readonly float[] _screenSizes;
    private USkeletalMesh _owner;

    public SkeletalMeshComponent(CSkeletalMesh skeletalMesh) : base(new Geometry(skeletalMesh.LODs.First()))
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh) : base(new Geometry(skeletalMesh.LODModels.First()))
    {
        _owner = skeletalMesh;
        var lodInfo = skeletalMesh.GetOrDefault<FStructFallback[]>("LODInfo", []);
        _screenSizes = new float[lodInfo.Length];
        for (int i = 0; i < lodInfo.Length; i++)
        {
            _screenSizes[i] = 1 - lodInfo[i].Get<TPerPlatformProperty.FPerPlatformFloat>("ScreenSize").Default;
        }
    }

    public override void Update()
    {
        if (Actor is not MeshActor actor || Actor.ActorManager is not SceneSystem { ActiveCamera: {} camera }) return;

        var screenSize = actor.SphereCullingComponent.GetScreenSpaceCoverage(camera);

        var currentLodIndex = LODIndex;
        for (int i = 0; i < _screenSizes.Length; i++)
        {
            if (screenSize >= _screenSizes[i])
            {
                currentLodIndex = i;
                break;
            }
        }

        if (currentLodIndex != LODIndex)
        {
            Console.WriteLine("Switching LOD from {0} to {1}", LODIndex, currentLodIndex);
            LODIndex = currentLodIndex;

            var lod = _owner.LODModels[LODIndex];
            var geometry = new Geometry(lod);

            VBO.Bind();
            VBO.Update(geometry.Vertices);

            EBO.Bind();
            EBO.Update(geometry.Indices);
        }
    }

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
