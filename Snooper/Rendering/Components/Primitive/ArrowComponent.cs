using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Primitive;

public class ArrowComponent : DebugComponent
{
    public ArrowComponent(UArrowComponent component) : base(component)
    {
        // TODO:
    }

    public ArrowComponent(Transform? transform = null, string? name = null) : base(new Vector3(1.0f, 0.0f, 0.0f), 1, transform, name)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), () => new Geometry(Vector3.Zero, 0.1f, 3f, 1f, 0.25f));
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(Vector3 center, float shaftRadius, float length, float coneHeight, float coneRadius)
        {
            const int segments = 12; // Number of segments around the cylinder and cone

            var vertices = new List<Vector3>();

            // Calculate positions (arrow points along Z axis - forward, matching mesh facing direction)
            var shaftLength = length - coneHeight;
            var shaftStart = center with { Z = center.Z - length / 2 };
            var shaftEnd = shaftStart with { Z = shaftStart.Z + shaftLength };
            var coneBase = shaftEnd;
            var coneTip = coneBase with { Z = coneBase.Z + coneHeight };

            // Generate cylinder (shaft) rings
            var cylinderRings = new Vector3[2, segments];

            // Bottom ring of cylinder (XY plane perpendicular to Z)
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                var x = MathF.Cos(angle) * shaftRadius;
                var y = MathF.Sin(angle) * shaftRadius;
                cylinderRings[0, i] = new Vector3(shaftStart.X + x, shaftStart.Y + y, shaftStart.Z);
            }

            // Top ring of cylinder (at cone base)
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                var x = MathF.Cos(angle) * shaftRadius;
                var y = MathF.Sin(angle) * shaftRadius;
                cylinderRings[1, i] = new Vector3(shaftEnd.X + x, shaftEnd.Y + y, shaftEnd.Z);
            }

            // Draw vertical lines for cylinder shaft
            for (var i = 0; i < segments; i++)
            {
                vertices.Add(cylinderRings[0, i]);
                vertices.Add(cylinderRings[1, i]);
            }

            // Draw circumference rings for cylinder
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                vertices.Add(cylinderRings[0, i]);
                vertices.Add(cylinderRings[0, next]);

                vertices.Add(cylinderRings[1, i]);
                vertices.Add(cylinderRings[1, next]);
            }

            // Generate cone base ring
            var coneBaseRing = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                var x = MathF.Cos(angle) * coneRadius;
                var y = MathF.Sin(angle) * coneRadius;
                coneBaseRing[i] = new Vector3(coneBase.X + x, coneBase.Y + y, coneBase.Z);
            }

            // Draw cone base circumference
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                vertices.Add(coneBaseRing[i]);
                vertices.Add(coneBaseRing[next]);
            }

            // Draw lines from cone base to tip
            for (var i = 0; i < segments; i++)
            {
                vertices.Add(coneBaseRing[i]);
                vertices.Add(coneTip);
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
