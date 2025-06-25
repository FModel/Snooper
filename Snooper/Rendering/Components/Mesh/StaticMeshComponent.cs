using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public sealed override int LodCount => _mesh.LODs.Count;
    public sealed override float[] ScreenSizes { get; }

    private readonly CStaticMesh _mesh;

    public StaticMeshComponent(UStaticMesh owner, CStaticMesh mesh) : base(mesh.LODs[0])
    {
        _mesh = mesh;

        ScreenSizes = owner.RenderData?.ScreenSize ?? [];
    }

    protected override IVertexData GetPrimitive(int index) => new Geometry(_mesh.LODs[index]);
}
