using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class LandscapeSplinesComponent : SpatialComponent
{
    private readonly List<USplineMeshComponent> _splines = [];

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
                        _splines.Add(splineMesh);
                    }
                }
            }
        }
    }

    protected override void OnActorAttached(Actor actor)
    {
        base.OnActorAttached(actor);

        foreach (var spline in _splines)
        {
            if (spline.GetStaticMesh().TryLoad<UStaticMesh>(out var mesh))
            {
                actor.Components.Add(new SplineMeshComponent(mesh, spline) { Relation = this });
            }
        }
    }
}
