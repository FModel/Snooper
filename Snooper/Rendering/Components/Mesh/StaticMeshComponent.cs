using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public StaticMeshComponent(UStaticMesh staticMesh) : base(null, staticMesh.Name)
    {
        SetGeometry(staticMesh);
    }
    
    public StaticMeshComponent(UStaticMesh staticMesh, UStaticMeshComponent component) : base(component)
    {
        SetGeometry(staticMesh);
        // TODO: use component.LODData to override some stuff (eg vertex colors)
    }
    
    private void SetGeometry(UStaticMesh staticMesh)
    {
        if (!staticMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
        
        using (mesh)
        {
            SetGeometry(staticMesh, mesh);
        }
    }
}
