using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Mesh;

public class InstancedStaticMeshComponent : StaticMeshComponent
{
    public InstancedStaticMeshComponent(UStaticMesh owner, CStaticMesh mesh, FTransform relation, FInstancedStaticMeshInstanceData[] instances) : base(owner, mesh)
    {
        if (instances.Length == 0)
            return;
        
        LocalInstanceTransforms.Clear();
        foreach (var data in instances)
        {
            LocalInstanceTransforms.Add(data.TransformData * relation);
        }
    }
}