using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    public SkeletalMeshComponent(USkeletalMesh skeletalMesh) : base(null, skeletalMesh.Name)
    {
        SetGeometry(skeletalMesh);
    }
    
    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, USkeletalMeshComponent component) : base(component)
    {
        SetGeometry(skeletalMesh);
    }

    private void SetGeometry(USkeletalMesh skeletalMesh)
    {
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));
        
        using (mesh)
        {
            SetGeometry(skeletalMesh, mesh);
        }
    }
}
