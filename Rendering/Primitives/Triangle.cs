using System.Numerics;

namespace Snooper.Rendering.Primitives;

public struct Triangle() : IPrimitiveData
{
    public Vector3[] Vertices { get; } =
    [
        new(-0.5f, -0.5f, 0.0f),
        new(0.5f, -0.5f, 0.0f),
        new(0.0f,  0.5f, 0.0f)
    ];

    public uint[] Indices { get; } =
    [
        0, 1, 2
    ];
}
