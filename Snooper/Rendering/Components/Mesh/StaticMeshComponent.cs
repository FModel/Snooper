using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    private readonly CStaticMesh _mesh;

    public StaticMeshComponent(UStaticMesh owner, CStaticMesh mesh) : base(mesh.LODs, owner.Materials, mesh.BoundingBox)
    {
        _mesh = mesh;
    }
    
    public StaticMeshComponent(ALandscapeProxy owner, CStaticMesh mesh) : base(mesh.LODs, [owner.LandscapeMaterial.ResolvedObject], mesh.BoundingBox)
    {
        _mesh = mesh;
    }
}
