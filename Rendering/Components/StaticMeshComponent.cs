using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Core;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Plane = System.Numerics.Plane;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(RenderSystem))]
public sealed class StaticMeshComponent : PrimitiveComponent
{
    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(Plane plane)
        {
            // Normal of the plane
            Vector3 normal = plane.Normal;

            // Calculate two vectors that lie on the plane and are perpendicular to the normal
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

            Vector3 basis2 = Vector3.Cross(normal, basis1);

            // Calculate the center of the plane (point on the plane)
            Vector3 center = -normal * plane.D;

            // Define the four vertices of the quad
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

        public Geometry(CStaticMeshLod lod)
        {
            Vertices = lod.Verts.Select(x => new Vector3(x.Position.X, x.Position.Z, x.Position.Y) * 0.01f).ToArray();

            Indices = new uint[lod.Indices.Value.Length];
            for (int i = 0; i < Indices.Length; i++)
            {
                Indices[i] = (uint) lod.Indices.Value[i];
            }
        }

        public Geometry(CSkelMeshLod lod)
        {
            Vertices = lod.Verts.Select(x => new Vector3(x.Position.X, x.Position.Z, x.Position.Y) * 0.01f).ToArray();

            Indices = new uint[lod.Indices.Value.Length];
            for (int i = 0; i < Indices.Length; i++)
            {
                Indices[i] = (uint) lod.Indices.Value[i];
            }
        }

        public Geometry(FBox box)
        {
            // Create a cube from the bounding box
            Vector3 min = box.Min * 0.01f;
            Vector3 max = box.Max * 0.01f;

            Vertices =
            [
                new Vector3(min.X, min.Z, min.Y),
                new Vector3(max.X, min.Z, min.Y),
                new Vector3(max.X, max.Z, min.Y),
                new Vector3(min.X, max.Z, min.Y),
                new Vector3(min.X, min.Z, max.Y),
                new Vector3(max.X, min.Z, max.Y),
                new Vector3(max.X, max.Z, max.Y),
                new Vector3(min.X, max.Z, max.Y)
            ];
            Indices =
            [
                0, 1, 2, 0, 2, 3, // Bottom face
                4, 5, 6, 4, 6, 7, // Top face
                0, 1, 5, 0, 5, 4, // Front face
                2, 3, 7, 2, 7, 6, // Back face
                0, 3, 7, 0, 7, 4, // Left face
                1, 2, 6, 1, 6, 5 // Right face
            ];
        }
    }

    private FBox _boundingBox;

    public StaticMeshComponent() : base(new Geometry(new Plane(Vector3.UnitZ, 0)))
    {

    }

    public StaticMeshComponent(IPrimitiveData data) : base(data)
    {

    }

    public StaticMeshComponent(CStaticMesh convertedMesh) : base(new Geometry(convertedMesh.LODs[0]))
    {
        _boundingBox = convertedMesh.BoundingBox  * 0.01f;
    }

    public StaticMeshComponent(CSkeletalMesh convertedMesh) : base(new Geometry(convertedMesh.LODs[0]))
    {
        _boundingBox = convertedMesh.BoundingBox * 0.01f;
    }

    public StaticMeshComponent(FBox box) : base(new Geometry(box))
    {
        _boundingBox = box * 0.01f;
        PolygonMode = OpenTK.Graphics.OpenGL4.PolygonMode.Line;
    }

    public bool IsInFrustum(CameraComponent cameraComponent)
    {
        _boundingBox.GetCenterAndExtents(out var center, out var extents);
        (center.Y, center.Z) = (center.Z, center.Y); // Swap Y and Z for OpenGL compatibility
        (extents.Y, extents.Z) = (extents.Z, extents.Y); // Swap Y and Z for OpenGL compatibility

        var frustum = cameraComponent.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }

        // Check if the bounding box is outside any of the frustum planes
        foreach (var plane in frustum)
        {
            // Calculate the distance from the center of the bounding box to the plane
            float distance = Vector3.Dot(plane.Normal, center) + plane.D;
            float radius = Vector3.Dot(extents, Vector3.Abs(plane.Normal));

            // If the distance is less than -radius, the box is outside the plane
            if (distance < -radius)
            {
                return false; // The box is outside the frustum
            }
        }

        // If the box is not outside any plane, it is in the frustum
        return true;
    }
}
