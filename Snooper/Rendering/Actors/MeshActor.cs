using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class MeshActor : Actor
{
    public CullingComponent CullingComponent { get; }
    public MeshComponent MeshComponent { get; }
    
    public MeshActor(UStaticMesh staticMesh, TransformComponent? transform = null) : base(staticMesh.LightingGuid, staticMesh.Name, transform)
    {
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
        if (staticMesh.RenderData?.Bounds is null)
            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

        CullingComponent = new SphereCullingComponent(staticMesh.RenderData.Bounds);
        MeshComponent = new StaticMeshComponent(staticMesh, mesh);
        
        Components.Add(CullingComponent);
        Components.Add(MeshComponent);
    }
    
    public MeshActor(FTransform relation, UStaticMesh staticMesh, params FInstancedStaticMeshInstanceData[] transforms) : this(staticMesh, transforms[0].TransformData * relation)
    {
        for (var i = 1; i < transforms.Length; i++)
        {
            InstancedTransforms.AddLocalInstance(transforms[i].TransformData * relation);
        }
    }

    public MeshActor(USkeletalMesh skeletalMesh, TransformComponent? transform = null) : base(new FGuid((uint) skeletalMesh.GetFullName().GetHashCode()), skeletalMesh.Name, transform)
    {
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

        CullingComponent = new SphereCullingComponent(skeletalMesh.ImportedBounds);
        MeshComponent = new SkeletalMeshComponent(skeletalMesh, mesh);
        
        Components.Add(CullingComponent);
        Components.Add(MeshComponent);
    }
}
