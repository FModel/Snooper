using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class InstancedStaticMeshComponent : StaticMeshComponent
{
    public readonly List<Transform> LocalInstancedTransforms = [];

    public InstancedStaticMeshComponent(UInstancedStaticMeshComponent component, UStaticMesh staticMesh) : base(component, staticMesh)
    {
        var instances = component.GetInstances();
        foreach (var data in instances)
        {
            LocalInstancedTransforms.Add(data.TransformData);
        }
        
        if (LocalInstancedTransforms.Count == 0)
        {
            // add a dummy instance to avoid issues with empty instance arrays
            LocalInstancedTransforms.Add(Transform.Identity);
        }
    }

    protected override Matrix4x4[] GetInstanceMatrices()
    {
        var matrices = new Matrix4x4[LocalInstancedTransforms.Count];
        for (var i = 0; i < LocalInstancedTransforms.Count; i++)
        {
            matrices[i] = LocalInstancedTransforms[i].ToMatrix() * WorldMatrix;
        }
        return matrices;
    }
}