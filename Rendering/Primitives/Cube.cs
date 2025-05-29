using System.Numerics;

namespace Snooper.Rendering.Primitives;

public struct Cube() : IPrimitiveData
{
    public Vector3[] Vertices { get; } =
    [
        new(-0.5f, -0.5f, -0.5f),
        new( 0.5f, -0.5f, -0.5f),
        new( 0.5f,  0.5f, -0.5f),
        new(-0.5f,  0.5f, -0.5f),

        new(-0.5f, -0.5f,  0.5f),
        new( 0.5f, -0.5f,  0.5f),
        new( 0.5f,  0.5f,  0.5f),
        new(-0.5f,  0.5f,  0.5f)
    ];

    public uint[] Indices { get; } =
    [
        0, 1, 2,
        2, 3, 0,

        4, 6, 5,
        6, 4, 7,

        4, 0, 3,
        3, 7, 4,

        1, 5, 6,
        6, 2, 1,

        3, 2, 6,
        6, 7, 3,

        4, 5, 1,
        1, 0, 4
    ];
}
