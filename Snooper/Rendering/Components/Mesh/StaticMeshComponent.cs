using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public StaticMeshComponent(UStaticMesh owner, CStaticMesh mesh, Transform? transform = null, string? name = null) : base(owner.LightingGuid, mesh.LODs, owner.Materials, mesh.BoundingBox, transform, name ?? owner.Name)
    {
        
    }
    
    public StaticMeshComponent(ALandscapeProxy owner, CStaticMesh mesh) : base(FGuid.Random(), mesh.LODs, [owner.LandscapeMaterial.ResolvedObject], mesh.BoundingBox)
    {
        
    }

    public StaticMeshComponent(UStaticMeshComponent component, UStaticMesh staticMesh) : base(component)
    {
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
        if (staticMesh.RenderData?.Bounds is null)
            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

        staticMesh.OverrideMaterials(component.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []));
        MaterialPointers = staticMesh.Materials;
        
        using (mesh)
        {
            LevelOfDetails = CreateGeometry(staticMesh.LightingGuid, mesh.LODs);
            // TODO: use component.LODData to override some stuff (eg vertex colors)
            Bounds = mesh.BoundingBox;
        }
    }
}
