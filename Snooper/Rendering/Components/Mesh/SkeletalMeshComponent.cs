using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    private readonly CSkeletalMesh _mesh;

    public SkeletalMeshComponent(USkeletalMesh owner, CSkeletalMesh mesh, Transform? transform = null, string? name = null) : base(FGuid.Random(), mesh.LODs, owner.Materials, mesh.BoundingBox, transform, name ?? owner.Name)
    {
        _mesh = mesh;
    }
}
