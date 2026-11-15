using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Visualization;

public interface IVisualization<T> where T : SpatialComponent
{
    public static abstract T[] Create(ActorComponent owner);
}
