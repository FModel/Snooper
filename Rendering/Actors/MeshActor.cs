using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Culling;

namespace Snooper.Rendering.Actors;

public class MeshActor : Actor
{
    public MeshActor(UStaticMesh staticMesh) : base(staticMesh.Name)
    {
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
        if (staticMesh.RenderData?.Bounds is null)
            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

        CullingComponent = new SphereCullingComponent(staticMesh.RenderData.Bounds);

        Components.Add(new StaticMeshComponent(staticMesh, mesh));
        Components.Add(CullingComponent);
    }

    public MeshActor(USkeletalMesh skeletalMesh) : base(skeletalMesh.Name)
    {
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

        CullingComponent = new SphereCullingComponent(skeletalMesh.ImportedBounds);

        Components.Add(new SkeletalMeshComponent(skeletalMesh, mesh));
        Components.Add(CullingComponent);
    }

    public CullingComponent CullingComponent { get; }
}
