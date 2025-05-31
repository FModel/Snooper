using System.Numerics;

namespace Snooper.Rendering.Primitives;

public readonly struct Plane : IPrimitiveData
{
    public Vector3[] Vertices { get; }
    public uint[] Indices { get; }

    public Plane(Vector3 normal, float distance)
    {
        var plane = new System.Numerics.Plane(normal, distance);

        Vector3 basis1;
        if (normal.X != 0 || normal.Y != 0)
        {
            basis1 = new Vector3(normal.Y, -normal.X, 0);
        }
        else
        {
            basis1 = new Vector3(0, normal.Z, -normal.Y);
        }
        basis1 = Vector3.Normalize(basis1);

        var center = -normal * plane.D;
        var basis2 = Vector3.Cross(normal, basis1);

        Vertices = new Vector3[4];
        Vertices[0] = center - basis1 - basis2;
        Vertices[1] = center + basis1 - basis2;
        Vertices[2] = center + basis1 + basis2;
        Vertices[3] = center - basis1 + basis2;

        Indices =
        [
            0, 1, 2,
            0, 2, 3
        ];
    }
}
