using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class LandscapeSplinesComponent : SpatialComponent
{
    private readonly List<USplineMeshComponent> _components = [];

    public LandscapeSplinesComponent(ULandscapeSplinesComponent component) : base(component)
    {
        foreach (var ptr in component.Segments)
        {
            if (ptr?.TryLoad<ULandscapeSplineSegment>(out var segment) == true)
            {
                foreach (var meshPtr in segment.LocalMeshComponents)
                {
                    if (meshPtr?.TryLoad<USplineMeshComponent>(out var splineMesh) == true)
                    {
                        _components.Add(splineMesh);
                    }
                }
            }
        }
    }

    protected override void BeginPlay(ActorManager scene)
    {
        base.BeginPlay(scene);
        if (Actor is not { } actor) return;

        foreach (var component in _components)
        {
            if (!component.GetStaticMesh().TryLoad<UStaticMesh>(out var mesh)) continue;
            actor.Components.Add(new SplineMeshComponent(mesh, component) { Relation = this });
        }
    }

    protected override void EndPlay(EEndPlayReason reason)
    {
        base.EndPlay(reason);
        if (Actor is not { } actor) return;

        foreach (var spline in Children.OfType<SplineMeshComponent>().ToArray())
        {
            actor.Components.Remove(spline);
        }
    }
}
