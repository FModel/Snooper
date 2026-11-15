using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Components.Visualization;

public class LandscapeMeshComponentVisualization : BoxComponent, IVisualization<LandscapeMeshComponentVisualization>
{
    public static LandscapeMeshComponentVisualization[] Create(ActorComponent owner) => owner is LandscapeMeshComponent landscape ? [new LandscapeMeshComponentVisualization(landscape)] : [];

    private LandscapeMeshComponentVisualization(LandscapeMeshComponent landscape) : base(landscape.Descriptor.Bounds, Settings.LandscapeBounds, name: $"{landscape.Name} (Bounds)")
    {

    }
}
