using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class LandscapeProxyActor : Actor
{
    public LandscapeProxyActor(ALandscapeProxy landscape, TransformComponent? transform = null, bool convert = false) : base(landscape.Name, transform: transform)
    {
        var components = landscape.GetOrDefault<ULandscapeComponent[]>("LandscapeComponents", []);
        foreach (var component in components)
        {
            Children.Add(convert ? new MeshActor(landscape, component) : new LandscapeActor(component));
        }
    }
}
