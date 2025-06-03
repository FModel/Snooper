using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(DebugSystem))]
public class DebugComponent(IPrimitiveData primitive) : PrimitiveComponent(primitive)
{
    protected override PolygonMode PolygonMode { get => PolygonMode.Line; }

    public DebugComponent(BoxCullingComponent box) : this(new Geometry(box))
    {

    }

    public DebugComponent(SphereCullingComponent sphere) : this(new Geometry(sphere))
    {

    }

    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(FBox box)
        {
            box *= Settings.GlobalScale;
            var min = box.Min;
            var max = box.Max;

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

        public Geometry(BoxCullingComponent box)
        {
            var center = box.Center;
            var extents = box.Extents;

            Vertices =
            [
                new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z - extents.Z),
                new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z - extents.Z),
                new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z - extents.Z),
                new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z - extents.Z),
                new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z + extents.Z),
                new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z + extents.Z),
                new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z + extents.Z),
                new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z + extents.Z)
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

        public Geometry(SphereCullingComponent sphere)
        {
            // generate a sphere with a given radius and an origin vector3
            // using System.Numerics.Vector3 for the vertices

            var center = sphere.Origin;
            var radius = sphere.Radius;

            const int segments = 16; // number of segments for the sphere
            const int rings = 16; // number of rings for the sphere
            var vertices = new List<Vector3>();
            for (int i = 0; i <= rings; i++)
            {
                float theta = MathF.PI * i / rings;
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);

                for (int j = 0; j <= segments; j++)
                {
                    float phi = 2 * MathF.PI * j / segments;
                    float sinPhi = MathF.Sin(phi);
                    float cosPhi = MathF.Cos(phi);

                    float x = center.X + radius * cosPhi * sinTheta;
                    float y = center.Y + radius * cosTheta;
                    float z = center.Z + radius * sinPhi * sinTheta;

                    vertices.Add(new Vector3(x, y, z));
                }
            }
            Vertices = vertices.ToArray();

            var indices = new List<uint>();
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int first = (i * (segments + 1)) + j;
                    int second = first + segments + 1;

                    indices.Add((uint)first);
                    indices.Add((uint)second);
                    indices.Add((uint)(first + 1));

                    indices.Add((uint)second);
                    indices.Add((uint)(second + 1));
                    indices.Add((uint)(first + 1));
                }
            }
            Indices = indices.ToArray();
        }
    }
}
