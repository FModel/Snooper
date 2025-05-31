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
            var center = box.BoxCenter;
            var extents = box.BoxExtents;

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
    }
}
