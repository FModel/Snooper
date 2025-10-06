using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Misc;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    public SkeletalMeshComponent(USkeletalMesh skeletalMesh) : base(skeletalMesh.Materials, null, skeletalMesh.Name)
    {
        Path = skeletalMesh.Name;
        
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

        using (mesh)
        {
            SetGeometry(FGuid.Random(), mesh.LODs, mesh.BoundingBox);
        }
    }
    
    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, USkeletalMeshComponent component) : base(component)
    {
        Path = skeletalMesh.Name;
        
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

        MaterialsToParse = skeletalMesh.Materials;

        using (mesh)
        {
            SetGeometry(FGuid.Random(), mesh.LODs, mesh.BoundingBox);
        }
    }
}
