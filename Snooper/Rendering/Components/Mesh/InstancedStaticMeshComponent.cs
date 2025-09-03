using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Mesh;

public class InstancedStaticMeshComponent : StaticMeshComponent
{
    public InstancedStaticMeshComponent(UStaticMesh owner, CStaticMesh mesh, FInstancedStaticMeshInstanceData[] instances, FTransform transform, string? name = null) : base(owner, mesh, null, name)
    {
        if (instances.Length == 0)
            return;
        
        LocalInstanceTransforms.Clear();
        foreach (var data in instances)
        {
            LocalInstanceTransforms.Add(data.TransformData * transform);
        }
    }
}