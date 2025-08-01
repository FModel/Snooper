using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Actors;

public class LandscapeActor : Actor
{
    public LandscapeMeshComponent MeshComponent { get; }
    
    public LandscapeActor(ULandscapeComponent component) : base(component.Name, component.MapBuildDataId, component.GetRelativeTransform())
    {
        MeshComponent = new LandscapeMeshComponent(component);
        
        Components.Add(MeshComponent);
    }
    
    internal override string Icon => "mountain";
}