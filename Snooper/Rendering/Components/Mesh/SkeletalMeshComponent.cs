using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    public SkeletalMeshComponent(USkeletalMesh owner, CSkeletalMesh mesh, Transform? transform = null, string? name = null) : base(FGuid.Random(), mesh.LODs, owner.Materials, mesh.BoundingBox, transform, name ?? owner.Name)
    {
        
    }

    public SkeletalMeshComponent(USkeletalMeshComponent component, USkeletalMesh skeletalMesh) : base(component)
    {
        if (!skeletalMesh.TryConvert(out var mesh))
            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));
        
        MaterialPointers = skeletalMesh.Materials;

        using (mesh)
        {
            LevelOfDetails = CreateGeometry(FGuid.Random(), mesh.LODs);
            Bounds = mesh.BoundingBox;
        }
    }
}
