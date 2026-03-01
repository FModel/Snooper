using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class LandscapeSplinesComponent : SpatialComponent
{
    private readonly List<USplineMeshComponent> _splines = [];

    public LandscapeSplinesComponent(ULandscapeSplinesComponent component) : base(component)
    {
        var ptrs = component.GetOrDefault<FPackageIndex?[]>("Segments", []);
        foreach (var ptr in ptrs)
        {
            if (ptr?.TryLoad<UObject>(out var segment) == true)
            {
                var meshPtrs = segment.GetOrDefault<FPackageIndex?[]>("LocalMeshComponents", []);
                foreach (var meshPtr in meshPtrs)
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
