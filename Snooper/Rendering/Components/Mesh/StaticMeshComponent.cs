using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public sealed override int LodCount => _mesh.LODs.Count;
    public sealed override float[] ScreenSizes { get; }

    private readonly CStaticMesh _mesh;

    public StaticMeshComponent(UStaticMesh owner, CStaticMesh mesh) : base(mesh.LODs[0], owner.Materials)
    {
        _mesh = mesh;

        ScreenSizes = owner.RenderData?.ScreenSize ?? [];
    }
    
    public StaticMeshComponent(ALandscapeProxy owner, CStaticMesh mesh) : base(mesh.LODs[0], [owner.LandscapeMaterial.ResolvedObject])
    {
        _mesh = mesh;

        ScreenSizes = [];
    }

    protected override IVertexData GetPrimitive(int index) => new Geometry(_mesh.LODs[index]);
}
