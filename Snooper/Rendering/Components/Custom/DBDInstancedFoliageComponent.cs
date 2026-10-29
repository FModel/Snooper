using CUE4Parse.GameTypes.DBD.Assets.Exports.Components;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Custom;

public class DBDInstancedFoliageComponent(UDBDInstancedFoliageComponent foliage) : ContainerComponent(foliage, [.. CreateMeshes(foliage)])
{
    private static IEnumerable<StaticMeshComponent> CreateMeshes(UDBDInstancedFoliageComponent foliage)
    {
        foreach (var (mesh, overrides, transforms) in foliage.GetMeshes())
        {
            if (!mesh.TryLoad<UStaticMesh>(out var staticMesh))
                continue;

            var component = new InstancedStaticMeshComponent(staticMesh, [.. transforms.Select(transform => (Transform) transform)]);
            for (var index = 0; index < Math.Min(overrides.Length, staticMesh.Materials.Length); index++)
            {
                if (overrides[index] is { IsNull: false } material)
                    component.SetMaterial(index, material);
            }

            yield return component;
        }
    }
}
