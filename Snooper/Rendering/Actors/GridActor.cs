using Snooper.Rendering.Components;

namespace Snooper.Rendering.Actors;

public class GridActor : Actor
{
    public GridActor(bool transparent) : base("Grid")
    {
        Components.Add(transparent ? new GridComponent() : new OpaqueGridComponent());
    }
}
