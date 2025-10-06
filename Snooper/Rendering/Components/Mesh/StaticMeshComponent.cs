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

        using (mesh)
        {
            SetGeometry(staticMesh.LightingGuid, mesh.LODs, mesh.BoundingBox);
        }
    }
    
    public StaticMeshComponent(UStaticMesh staticMesh, UStaticMeshComponent component) : base(component)
    {
        Path = staticMesh.Name;
        
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));

        staticMesh.OverrideMaterials(component.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []));
        MaterialsToParse = staticMesh.Materials;

        using (mesh)
        {
            SetGeometry(staticMesh.LightingGuid, mesh.LODs, mesh.BoundingBox);
            // TODO: use component.LODData to override some stuff (eg vertex colors)
        }
    }
}
