using System.Numerics;
using Snooper.Core;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(GridSystem))]
public class GridComponent() : PrimitiveComponent(new Geometry())
{
    internal override string Icon => "grid";

    private class Geometry : PrimitiveData
    {
        public Geometry()
        {
            Vertices =
            [
                new Vector3(1.0f, 1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f, 1.0f, 0.0f)
            ];
            
            Indices =
            [
                0, 1, 3,
                1, 2, 3
            ];
        }
    }
}

public class OpaqueGridComponent : GridComponent;