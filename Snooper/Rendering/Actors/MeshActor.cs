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

    public MeshActor(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh)
    {
        Components.Add(new SkeletalMeshComponent(skeletalMesh, transform));
    }
}
