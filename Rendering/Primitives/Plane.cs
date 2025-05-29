namespace Snooper.Rendering.Primitives;

public struct Plane() : IPrimitiveData
{
    public float[] Vertices { get; } =
    [
        0.5f,  0.5f, 0.0f,
        0.5f, -0.5f, 0.0f,
        -0.5f, -0.5f, 0.0f,
        -0.5f,  0.5f, 0.0f
    ];

    public uint[] Indices { get; } =
    [
        0, 1, 3,
        1, 2, 3
    ];
}
