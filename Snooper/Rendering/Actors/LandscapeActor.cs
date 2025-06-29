using CUE4Parse_Conversion.Landscape;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class LandscapeActor : Actor
{
    public LandscapeActor(ALandscapeProxy landscape, TransformComponent? transform = null) : base(System.Guid.NewGuid(), landscape.Name, transform)
    {
        Components.Add(new LandscapeMeshComponent(landscape));
    }
}
