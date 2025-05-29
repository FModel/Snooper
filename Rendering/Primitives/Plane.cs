using System.Numerics;

namespace Snooper.Rendering.Primitives;

public struct Plane() : IPrimitiveData
{
    public Vector3[] Vertices { get; } =
    [
        new(0.5f, 0.5f, 0.0f),
        new(0.5f, -0.5f, 0.0f),
        new(-0.5f, -0.5f, 0.0f),
        new(-0.5f, 0.5f, 0.0f)
    ];

    public uint[] Indices { get; } =
    [
        0, 1, 3,
        1, 2, 3
    ];
}
