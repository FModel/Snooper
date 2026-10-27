using CUE4Parse.GameTypes.Nascar.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.GeometryCollection;
using CUE4Parse.UE4.Assets.Exports.Houdini;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class MeshActor : Actor
{
    public MeshActor(UStaticMesh staticMesh, Transform? transform = null) : base(staticMesh)
    {
        AddMeshComponent(new StaticMeshComponent(staticMesh, transform));
    }

    public MeshActor(UIRMesh staticMesh, Transform? transform = null) : base(staticMesh)
    {
        for (var i = 0u; i < staticMesh.Info.PartCount; i++)
        {
            AddMeshComponent(new StaticMeshComponent(staticMesh, i, null, transform));
        }
    }

    public MeshActor(UHoudiniStaticMesh staticMesh, Transform? transform = null) : base(staticMesh)
    {
        AddMeshComponent(new StaticMeshComponent(staticMesh, transform));
    }

    public MeshActor(UGeometryCollection geometryCollection, Transform? transform = null) : base(geometryCollection)
    {
        AddMeshComponent(new GeometryCollectionComponent(geometryCollection, transform));
    }

    public MeshActor(USkinnedAsset skinnedAsset, Transform? transform = null) : base(skinnedAsset)
    {
        AddMeshComponent(new SkeletalMeshComponent(skinnedAsset, transform));
    }

    public MeshActor(UAnimationAsset animation, float playPosition = 0f, float playRate = 1f) : base(animation)
    {
        AddMeshComponent(new SkeletalMeshComponent(animation, playPosition, playRate));
    }

    private void AddMeshComponent(MeshComponent component)
    {
        if (component.Descriptor.Lods.Length > 0)
        {
            Components.Add(component);
        }
    }
}
