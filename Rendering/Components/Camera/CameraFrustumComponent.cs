using System.Numerics;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Primitives;
using Plane = System.Numerics.Plane;

namespace Snooper.Rendering.Components.Camera;

public class CameraFrustumComponent(CameraComponent cameraComponent) : DebugComponent(new Geometry())
{
    private struct Geometry() : IPrimitiveData
    {
        public Vector3[] Vertices { get; } = new Vector3[8];
        public uint[] Indices { get; } =
        [
            0, 1, 2, 0, 2, 3, // Near plane
            4, 5, 6, 4, 6, 7, // Far plane
            0, 1, 5, 0, 5, 4, // Left side
            1, 2, 6, 1, 6, 5, // Right side
            2, 3, 7, 2, 7, 6, // Bottom side
            3, 0, 4, 3, 4, 7 // Top side
        ];
    }

    public override void Update(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<Vector3> vbo)
    {
        base.Update(commands, ebo, vbo);

        vbo.Update(CalculateFrustumVertices(cameraComponent.GetLocalFrustumPlanes()), DrawId);
    }

    private Vector3[] CalculateFrustumVertices(Plane[] frustumPlanes)
    {
        if (frustumPlanes.Length != 6)
        {
            throw new ArgumentException("A frustum must be defined by exactly six planes.");
        }

        Vector3[] vertices = new Vector3[8];

        // Define indices for the planes for clarity
        const int near = 0;
        const int far = 1;
        const int left = 2;
        const int right = 3;
        const int top = 4;
        const int bottom = 5;

        // Calculate the eight corners of the frustum
        vertices[0] = IntersectionPoint(frustumPlanes[near], frustumPlanes[left], frustumPlanes[top]);
        vertices[1] = IntersectionPoint(frustumPlanes[near], frustumPlanes[right], frustumPlanes[top]);
        vertices[2] = IntersectionPoint(frustumPlanes[near], frustumPlanes[right], frustumPlanes[bottom]);
        vertices[3] = IntersectionPoint(frustumPlanes[near], frustumPlanes[left], frustumPlanes[bottom]);

        vertices[4] = IntersectionPoint(frustumPlanes[far], frustumPlanes[left], frustumPlanes[top]);
        vertices[5] = IntersectionPoint(frustumPlanes[far], frustumPlanes[right], frustumPlanes[top]);
        vertices[6] = IntersectionPoint(frustumPlanes[far], frustumPlanes[right], frustumPlanes[bottom]);
        vertices[7] = IntersectionPoint(frustumPlanes[far], frustumPlanes[left], frustumPlanes[bottom]);

        return vertices;
    }

    private Vector3 IntersectionPoint(Plane a, Plane b, Plane c)
    {
        var v1 = a.D * Vector3.Cross(b.Normal, c.Normal);
        var v2 = b.D * Vector3.Cross(c.Normal, a.Normal);
        var v3 = c.D * Vector3.Cross(a.Normal, b.Normal);
        var vec = new Vector3(v1.X + v2.X + v3.X, v1.Y + v2.Y + v3.Y, v1.Z + v2.Z + v3.Z);

        var f = -Vector3.Dot(a.Normal, Vector3.Cross(b.Normal, c.Normal));
        return vec / f;
    }
}
