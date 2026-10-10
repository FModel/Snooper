using Snooper.Rendering.Components;

namespace Snooper.Rendering.Actors;

public class GridActor : Actor
{
    public GridActor(bool opaque) : base("Grid")
    {
        Components.Add(opaque ? new OpaqueGridComponent() : new GridComponent());
    }
}
