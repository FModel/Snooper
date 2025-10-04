using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class MeshActor : Actor
{
    public MeshActor(UStaticMesh staticMesh, Transform? transform = null) : base(staticMesh)
    {
        var component = new StaticMeshComponent(staticMesh);
        
        if (transform is not null)
            component.LocalTransform = transform;
        
        Components.Add(component);
    }

    public MeshActor(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh)
    {
        var component = new SkeletalMeshComponent(skeletalMesh);
        
        if (transform is not null)
            component.LocalTransform = transform;
        
        Components.Add(component);
    }
}
