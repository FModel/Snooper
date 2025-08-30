using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Misc;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    private readonly CSkeletalMesh _mesh;

    public SkeletalMeshComponent(USkeletalMesh owner, CSkeletalMesh mesh) : base(FGuid.Random(), mesh.LODs, owner.Materials, mesh.BoundingBox)
    {
        _mesh = mesh;
    }
}
