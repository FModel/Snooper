using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Primitive;

public class CapsuleComponent : ShapeComponent
{
    public CapsuleComponent(UCapsuleComponent component) : base(component)
    {
        Color ??= new Vector3(0.15f, 0.15f, 0.45f);

        const float defaultRadius = 0.5f;

        var radius = defaultRadius;
        if (component.TryGetValue(out float capsuleRadius, "CapsuleRadius"))
        {
            radius = capsuleRadius * Settings.GlobalScale;
        }

        var halfHeight = defaultRadius * 2.3f;
        if (component.TryGetValue(out float capsuleHalfHeight, "CapsuleHalfHeight"))
        {
            halfHeight = capsuleHalfHeight * Settings.GlobalScale;
        }

        var bounds = new CullingBounds(Vector3.Zero, new Vector3(radius, halfHeight, radius));
        Descriptor = new PrimitiveDescriptor<Vector3>(bounds, () => new Geometry(radius, halfHeight));

        Materials[0].InlineContainer = new MaterialDataContainer(Color.Value, LineThickness);
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(float radius, float halfHeight) : this(Vector3.Zero, radius, halfHeight)
        {

        }

        public Geometry(Vector3 center, float radius, float halfHeight)
        {
            const int segments = 32; // More segments for smoother circles

            var vertices = new List<Vector3>();

            // In Unreal, halfHeight includes the hemisphere caps
            // So the cylindrical part extends from (halfHeight - radius) to -(halfHeight - radius)
            var cylinderHalfHeight = halfHeight - radius;

            // Calculate the top and bottom centers of the cylindrical part (Y is up)
            var topCenter = center with { Y = center.Y + cylinderHalfHeight };
            var bottomCenter = center with { Y = center.Y - cylinderHalfHeight };

            // Draw bottom equator circle (XZ plane at bottom of cylinder)
            for (var i = 0; i < segments; i++)
            {
                var angle1 = 2.0f * MathF.PI * i / segments;
                var angle2 = 2.0f * MathF.PI * (i + 1) / segments;

                var p1 = new Vector3(
                    bottomCenter.X + radius * MathF.Cos(angle1),
                    bottomCenter.Y,
                    bottomCenter.Z + radius * MathF.Sin(angle1)
                );

                var p2 = new Vector3(
                    bottomCenter.X + radius * MathF.Cos(angle2),
                    bottomCenter.Y,
                    bottomCenter.Z + radius * MathF.Sin(angle2)
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Draw top equator circle (XZ plane at top of cylinder)
            for (var i = 0; i < segments; i++)
            {
                var angle1 = 2.0f * MathF.PI * i / segments;
                var angle2 = 2.0f * MathF.PI * (i + 1) / segments;

                var p1 = new Vector3(
                    topCenter.X + radius * MathF.Cos(angle1),
                    topCenter.Y,
                    topCenter.Z + radius * MathF.Sin(angle1)
                );

                var p2 = new Vector3(
                    topCenter.X + radius * MathF.Cos(angle2),
                    topCenter.Y,
                    topCenter.Z + radius * MathF.Sin(angle2)
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Draw hemisphere arcs - we'll draw 3 vertical arcs (like point light style)
            // Arc 1: XY plane (front/back)
            // Bottom hemisphere
            for (var i = 0; i < segments / 2; i++)
            {
                var angle1 = MathF.PI * i / (segments / 2); // 0 to PI
                var angle2 = MathF.PI * (i + 1) / (segments / 2);

                var p1 = new Vector3(
                    bottomCenter.X + radius * MathF.Cos(angle1),
                    bottomCenter.Y - radius * MathF.Sin(angle1),
                    bottomCenter.Z
                );

                var p2 = new Vector3(
                    bottomCenter.X + radius * MathF.Cos(angle2),
                    bottomCenter.Y - radius * MathF.Sin(angle2),
                    bottomCenter.Z
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Top hemisphere
            for (var i = 0; i < segments / 2; i++)
            {
                var angle1 = MathF.PI * i / (segments / 2); // 0 to PI
                var angle2 = MathF.PI * (i + 1) / (segments / 2);

                var p1 = new Vector3(
                    topCenter.X + radius * MathF.Cos(angle1),
                    topCenter.Y + radius * MathF.Sin(angle1),
                    topCenter.Z
                );

                var p2 = new Vector3(
                    topCenter.X + radius * MathF.Cos(angle2),
                    topCenter.Y + radius * MathF.Sin(angle2),
                    topCenter.Z
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Arc 2: YZ plane (left/right)
            // Bottom hemisphere
            for (var i = 0; i < segments / 2; i++)
            {
                var angle1 = MathF.PI * i / (segments / 2); // 0 to PI
                var angle2 = MathF.PI * (i + 1) / (segments / 2);

                var p1 = new Vector3(
                    bottomCenter.X,
                    bottomCenter.Y - radius * MathF.Sin(angle1),
                    bottomCenter.Z + radius * MathF.Cos(angle1)
                );

                var p2 = new Vector3(
                    bottomCenter.X,
                    bottomCenter.Y - radius * MathF.Sin(angle2),
                    bottomCenter.Z + radius * MathF.Cos(angle2)
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Top hemisphere
            for (var i = 0; i < segments / 2; i++)
            {
                var angle1 = MathF.PI * i / (segments / 2); // 0 to PI
                var angle2 = MathF.PI * (i + 1) / (segments / 2);

                var p1 = new Vector3(
                    topCenter.X,
                    topCenter.Y + radius * MathF.Sin(angle1),
                    topCenter.Z + radius * MathF.Cos(angle1)
                );

                var p2 = new Vector3(
                    topCenter.X,
                    topCenter.Y + radius * MathF.Sin(angle2),
                    topCenter.Z + radius * MathF.Cos(angle2)
                );

                vertices.Add(p1);
                vertices.Add(p2);
            }

            // Draw vertical lines connecting the hemispheres (along the cylindrical part)
            // Draw 4 vertical lines at cardinal directions
            for (var i = 0; i < 4; i++)
            {
                var angle = MathF.PI / 2.0f * i; // 0, 90, 180, 270 degrees

                // XZ plane vertical lines
                var bottomPoint = new Vector3(
                    bottomCenter.X + radius * MathF.Cos(angle),
                    bottomCenter.Y,
                    bottomCenter.Z + radius * MathF.Sin(angle)
                );

                var topPoint = new Vector3(
                    topCenter.X + radius * MathF.Cos(angle),
                    topCenter.Y,
                    topCenter.Z + radius * MathF.Sin(angle)
                );

                vertices.Add(bottomPoint);
                vertices.Add(topPoint);
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
