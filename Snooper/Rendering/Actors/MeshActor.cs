using CUE4Parse_Conversion.Landscape;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
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
    
    public MeshActor(UStaticMesh staticMesh, TransformComponent? transform = null) : base(staticMesh.Name, staticMesh.LightingGuid, transform)
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
    
    public MeshActor(UStaticMesh staticMesh, params FInstancedStaticMeshInstanceData[] transforms) : this(staticMesh, transforms[0].TransformData)
    {
        for (var i = 1; i < transforms.Length; i++)
        {
            InstancedTransform.AddLocalInstance(transforms[i].TransformData);
        }
    }
    
    public MeshActor(ALandscapeProxy landscape, ULandscapeComponent component) : base(component.Name, component.MapBuildDataId, component.GetRelativeTransform())
    {
        if (!landscape.TryConvert([component], ELandscapeExportFlags.Mesh, out var mesh, out _, out _))
            throw new ArgumentException("Failed to convert landscape mesh.", nameof(landscape));
            
        CullingComponent = new BoxCullingComponent(component.CachedLocalBox);
        MeshComponent = new StaticMeshComponent(landscape, mesh);
        
        Components.Add(CullingComponent);
        Components.Add(MeshComponent);
    }

    public MeshActor(USkeletalMesh skeletalMesh, TransformComponent? transform = null) : base(skeletalMesh.Name, transform: transform)
    {
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

        CullingComponent = new SphereCullingComponent(skeletalMesh.ImportedBounds);
        MeshComponent = new SkeletalMeshComponent(skeletalMesh, mesh);
        
        Components.Add(CullingComponent);
        Components.Add(MeshComponent);
    }

    internal override string Icon => MeshComponent is StaticMeshComponent ? "cube" : "bone";
}
