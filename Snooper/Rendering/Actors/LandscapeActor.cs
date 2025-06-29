using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Actors;

public class LandscapeActor : Actor
{
    public CullingComponent CullingComponent { get; }
    public LandscapeMeshComponent MeshComponent { get; }
    
    public LandscapeActor(ULandscapeComponent component) : base(component.MapBuildDataId, component.Name, component.GetRelativeTransform())
    {
        CullingComponent = new BoxCullingComponent(component.CachedLocalBox);
        MeshComponent = new LandscapeMeshComponent(component);
        
        Components.Add(CullingComponent);
        Components.Add(MeshComponent);
    }
}