using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Primitive;

public class SphereComponent : ShapeComponent
{
    public SphereComponent(USphereComponent component) : base(component)
    {
        Color ??= new Vector3(0.15f, 0.45f, 0.15f);

        var radius = 0.5f;
        if (component.TryGetValue(out float sphereRadius, "SphereRadius"))
        {
            radius = sphereRadius * Settings.GlobalScale;
        }

        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(radius), () => new Geometry(radius));

        Materials[0].InlineContainer = new MaterialDataContainer(Color.Value, LineThickness);
    }

    public SphereComponent(float radius, Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : base(color, lineThickness, transform, name)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(radius), () => new Geometry(radius));
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(float radius) : this(Vector3.Zero, radius)
        {

        }

        public Geometry(Vector3 center, float radius)
        {
            var vertices = new List<Vector3>();

            const int segments = 32; // More segments for smoother circles

            // Draw equator circle on XY plane (horizontal)
            for (var i = 0; i < segments; i++)
            {
                var angle1 = 2.0f * MathF.PI * i / segments;
                var angle2 = 2.0f * MathF.PI * (i + 1) / segments;

                var p1 = new Vector3(
                    center.X + radius * MathF.Cos(angle1),
                    center.Y + radius * MathF.Sin(angle1),
                    center.Z
                );

                var p2 = new Vector3(
                    center.X + radius * MathF.Cos(angle2),
                    center.Y + radius * MathF.Sin(angle2),
                    center.Z
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Draw YZ plane circle (perpendicular to X axis)
            for (var i = 0; i < segments; i++)
            {
                var angle1 = 2.0f * MathF.PI * i / segments;
                var angle2 = 2.0f * MathF.PI * (i + 1) / segments;

                var p1 = new Vector3(
                    center.X,
                    center.Y + radius * MathF.Cos(angle1),
                    center.Z + radius * MathF.Sin(angle1)
                );

                var p2 = new Vector3(
                    center.X,
                    center.Y + radius * MathF.Cos(angle2),
                    center.Z + radius * MathF.Sin(angle2)
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Draw XZ plane circle (perpendicular to Y axis)
            for (var i = 0; i < segments; i++)
            {
                var angle1 = 2.0f * MathF.PI * i / segments;
                var angle2 = 2.0f * MathF.PI * (i + 1) / segments;

                var p1 = new Vector3(
                    center.X + radius * MathF.Cos(angle1),
                    center.Y,
                    center.Z + radius * MathF.Sin(angle1)
                );

                var p2 = new Vector3(
                    center.X + radius * MathF.Cos(angle2),
                    center.Y,
                    center.Z + radius * MathF.Sin(angle2)
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            Vertices = vertices.ToArray();

            Indices = new uint[Vertices.Length];
            for (uint i = 0; i < Indices.Length; i++)
            {
                Indices[i] = i;
            }
        }
    }
}
