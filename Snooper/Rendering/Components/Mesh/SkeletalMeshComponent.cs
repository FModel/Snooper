using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    private readonly CSkeletalMesh _mesh;

    public SkeletalMeshComponent(USkeletalMesh owner, CSkeletalMesh mesh) : base(mesh.LODs[0], owner.Materials)
    {
        _mesh = mesh;
    }

    protected override IVertexData GetPrimitive(int index) => new Geometry(_mesh.LODs[index]);
}
