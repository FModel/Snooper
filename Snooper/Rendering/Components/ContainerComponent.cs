using CUE4Parse.GameTypes.Nascar.Assets.Exports;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components;

public class ContainerComponent : SpatialComponent
{
    private StaticMeshComponent[]? _children;

    public ContainerComponent(UIRMesh staticMesh, UIRMeshComponent component) : base(component)
    {
        var materials = component.OverrideMaterials.Length == staticMesh.Info.MaterialCount
            ? component.OverrideMaterials
            : staticMesh.Materials;

        _children = new StaticMeshComponent[staticMesh.Info.PartCount];
        for (var i = 0u; i < staticMesh.Info.PartCount; i++)
        {
            _children[i] = new StaticMeshComponent(staticMesh, i, materials);
        }
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
