using System.Numerics;
using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(GridSystem))]
public class GridComponent() : PrimitiveComponent(new Geometry())
{
    private readonly struct Geometry() : IPrimitiveData
    {
        public Vector3[] Vertices { get; } =
        [
            new(1.0f, 1.0f, 0.0f),
            new(1.0f, -1.0f, 0.0f),
            new(-1.0f, -1.0f, 0.0f),
            new(-1.0f, 1.0f, 0.0f)
        ];

        public uint[] Indices { get; } =
        [
            0, 1, 3,
            1, 2, 3
        ];
    }
}
