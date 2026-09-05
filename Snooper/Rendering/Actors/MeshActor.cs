using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.GeometryCollection;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class MeshActor : Actor
{
    public MeshActor(UStaticMesh staticMesh, Transform? transform = null) : base(staticMesh)
    {
        Components.Add(new StaticMeshComponent(staticMesh, transform));
    }

    public MeshActor(UGeometryCollection geometryCollection, Transform? transform = null) : base(geometryCollection)
    {
        Components.Add(new GeometryCollectionComponent(geometryCollection, transform));
    }

    public MeshActor(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh)
    {
        Components.Add(new SkeletalMeshComponent(skeletalMesh, transform));
    }

    public MeshActor(UAnimationAsset animation, float playPosition = 0f, float playRate = 1f) : base(animation)
    {
        Components.Add(new SkeletalMeshComponent(animation, playPosition, playRate));
    }
}
