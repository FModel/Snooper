using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    public sealed override int LodCount => _mesh.LODs.Count;
    public sealed override float[] ScreenSizes { get; }

    private readonly CSkeletalMesh _mesh;

    public SkeletalMeshComponent(USkeletalMesh owner, CSkeletalMesh mesh) : base(mesh.LODs[0], owner.Materials)
    {
        _mesh = mesh;

        var lodInfo = owner.GetOrDefault<FStructFallback[]>("LODInfo", []);
        ScreenSizes = new float[lodInfo.Length];
        for (var i = 0; i < lodInfo.Length; i++)
        {
            ScreenSizes[i] = lodInfo[i].Get<TPerPlatformProperty.FPerPlatformFloat>("ScreenSize").Default;
        }
    }

    protected override IVertexData GetPrimitive(int index) => new Geometry(_mesh.LODs[index]);
}
