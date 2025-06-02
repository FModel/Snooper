using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Culling;

namespace Snooper.Rendering.Actors;

public class MeshActor : Actor
{
    public MeshActor(UStaticMesh staticMesh) : base(staticMesh.Name)
    {
        if (staticMesh.RenderData?.Bounds is null)
            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

        BoxCullingComponent = new BoxCullingComponent(staticMesh.RenderData.Bounds.GetBox());
        SphereCullingComponent = new SphereCullingComponent(staticMesh.RenderData.Bounds);

        Components.Add(new StaticMeshComponent(staticMesh));
        Components.Add(BoxCullingComponent);
        Components.Add(SphereCullingComponent);
    }

    public MeshActor(USkeletalMesh skeletalMesh) : base(skeletalMesh.Name)
    {
        BoxCullingComponent = new BoxCullingComponent(skeletalMesh.ImportedBounds.GetBox());
        SphereCullingComponent = new SphereCullingComponent(skeletalMesh.ImportedBounds);

        Components.Add(new SkeletalMeshComponent(skeletalMesh));
        Components.Add(BoxCullingComponent);
        Components.Add(SphereCullingComponent);
    }

    public BoxCullingComponent BoxCullingComponent { get; }
    public SphereCullingComponent SphereCullingComponent { get; }
}
