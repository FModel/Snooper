using System.Numerics;

namespace Snooper.Rendering.Primitives;

public class Sphere : PrimitiveData
{
    public Sphere(int sectors = 36, int stacks = 18, float radius = 1.0f)
    {
        List<Vector3> vertices = [];
        List<uint> indices = [];

        float sectorStep = 2 * (float)Math.PI / sectors;
        float stackStep = (float)Math.PI / stacks;
        float sectorAngle, stackAngle, x, y, z, xy;

        for (int i = 0; i <= stacks; ++i)
        {
            stackAngle = (float)Math.PI / 2 - i * stackStep;
            xy = radius * (float)Math.Cos(stackAngle);
            z = radius * (float)Math.Sin(stackAngle);

            for (int j = 0; j <= sectors; ++j)
            {
                sectorAngle = j * sectorStep;

                x = xy * (float)Math.Cos(sectorAngle);
                y = xy * (float)Math.Sin(sectorAngle);
                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int i = 0; i < stacks; ++i)
        {
            for (int j = 0; j < sectors; ++j)
            {
                int first = i * (sectors + 1) + j;
                int second = first + sectors + 1;

                indices.Add((uint)first);
                indices.Add((uint)(second + 1));
                indices.Add((uint)(second));

                indices.Add((uint)first);
                indices.Add((uint)(first + 1));
                indices.Add((uint)(second + 1));
            }
        }

        Vertices = vertices.ToArray();
        Indices = indices.ToArray();
    }
}
