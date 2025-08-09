using System.Numerics;

namespace Snooper.Rendering.Primitives;

public class Triangle : PrimitiveData
{
    public Triangle()
    {
        Vertices =
        [
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3(0.5f, -0.5f, 0.0f),
            new Vector3(0.0f, 0.5f, 0.0f)
        ];

        Indices = [0, 1, 2];
    }
}
