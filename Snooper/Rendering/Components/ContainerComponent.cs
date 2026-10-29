using CUE4Parse.GameTypes.DBD.Assets.Exports.Components;
using CUE4Parse.GameTypes.Nascar.Assets.Exports;
using Snooper.Core.Managers;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components;

public class ContainerComponent : SpatialComponent
{
    private StaticMeshComponent[]? _children;

    public ContainerComponent(UIRMesh staticMesh, UIRMeshComponent component) : base(component)
    {
        _children = new StaticMeshComponent[staticMesh.Info.PartCount];
        for (var i = 0u; i < staticMesh.Info.PartCount; i++)
        {
            _children[i] = new StaticMeshComponent(staticMesh, i, component.OverrideMaterials);
        }
    }

    public ContainerComponent(UDBDInstancedFoliageComponent foliage) : base(foliage)
    {
        var children = new List<StaticMeshComponent>();
        foreach (var (mesh, overrides, transforms) in foliage.GetMeshes())
        {
            if (!mesh.TryLoad<UStaticMesh>(out var staticMesh)) continue;

            var instances = new List<Transform>(transforms.Length);
            foreach (var transform in transforms)
            {
                instances.Add(transform);
            }

            children.Add(new InstancedStaticMeshComponent(staticMesh, instances, overrides));
        }

        _children = [.. children];
    }

    protected override void BeginPlay(ActorManager scene)
    {
        base.BeginPlay(scene);

        if (Actor == null || _children == null)
        {
            return;
        }

        foreach (var child in _children)
        {
            child.Relation = this;
            Actor.Components.Add(child);
        }

        _children = null;
    }
}
