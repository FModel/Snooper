using System.Numerics;

namespace Snooper.Rendering.Primitives;

public class Cone : PrimitiveData
{
    public Cone(int sectors = 36, float height = 1.0f, float radius = 1.0f)
    {
        List<Vector3> vertices = [];
        List<uint> indices = [];

        // Add the apex of the cone
        vertices.Add(new Vector3(0.0f, height, 0.0f));

        // Generate vertices for the base of the cone
        float sectorStep = 2 * (float)Math.PI / sectors;
        float x, z;

        for (int i = 0; i <= sectors; ++i)
        {
            float sectorAngle = i * sectorStep;
            x = radius * (float)Math.Cos(sectorAngle);
            z = radius * (float)Math.Sin(sectorAngle);
            vertices.Add(new Vector3(x, 0.0f, z));
        }

        // Generate indices for the sides of the cone
        for (int i = 0; i < sectors; ++i)
        {
            indices.Add(0); // Apex
            indices.Add((uint)(i + 1));
            indices.Add((uint)(i + 2));
        }

        // Generate indices for the base of the cone
        uint baseCenterIndex = (uint)(vertices.Count / 3);
        vertices.Add(Vector3.Zero);

        for (int i = 0; i < sectors; ++i)
        {
            indices.Add(baseCenterIndex);
            indices.Add((uint)(i + 1));
            indices.Add((uint)(i + 2));
        }

        Vertices = vertices.ToArray();
        Indices = indices.ToArray();
    }
}
