using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.UObject;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public StaticMeshComponent(UStaticMesh staticMesh) : base(staticMesh.Materials, null, staticMesh.Name)
    {
        Path = staticMesh.Name;
        
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
        if (staticMesh.RenderData?.Bounds is null)
            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

        using (mesh)
        {
            LevelOfDetails = CreateGeometry(staticMesh.LightingGuid, mesh.LODs);
            Bounds = mesh.BoundingBox;
        }
    }
    
    public StaticMeshComponent(UStaticMesh staticMesh, UStaticMeshComponent component) : base(component)
    {
        Path = staticMesh.Name;
        
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
        if (staticMesh.RenderData?.Bounds is null)
            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

        staticMesh.OverrideMaterials(component.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []));
        MaterialsToParse = staticMesh.Materials;
        
        using (mesh)
        {
            LevelOfDetails = CreateGeometry(staticMesh.LightingGuid, mesh.LODs);
            // TODO: use component.LODData to override some stuff (eg vertex colors)
            Bounds = mesh.BoundingBox;
        }
    }
}
