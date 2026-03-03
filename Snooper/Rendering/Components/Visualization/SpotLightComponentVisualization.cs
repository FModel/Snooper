using System.Numerics;
using Snooper.Rendering.Components.Light;

namespace Snooper.Rendering.Components.Visualization;

public class SpotLightComponentVisualization(SpotLightComponent light) : LightComponentVisualization(light, Settings.SpotLight, () => new Geometry(light))
{
    private class Geometry : DebugGeometry
    {
        public Geometry(SpotLightComponent light)
        {
            var vertices = new List<Vector3>();

            // Get cone parameters
            var range = light.AttenuationRadius;
            var outerAngle = light.OuterConeAngle * MathF.PI / 180.0f;
            var innerAngle = light.InnerConeAngle * MathF.PI / 180.0f;

            const int segments = 16; // Number of segments around the cone

            // Cone apex at origin
            var apex = Vector3.Zero;

            // Calculate where the cone intersects the range sphere
            var coneEndDistance = range * MathF.Cos(outerAngle);
            var coneEndRadius = range * MathF.Sin(outerAngle);

            // Outer cone circle at the intersection
            var outerCircle = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                outerCircle[i] = new Vector3(
                    coneEndDistance,
                    coneEndRadius * MathF.Cos(angle),
                    coneEndRadius * MathF.Sin(angle)
                );
            }

            // Draw cone edges from apex to circle (8 lines)
            for (var i = 0; i < segments; i += segments / 8)
            {
                vertices.Add(apex);
                vertices.Add(outerCircle[i]);
            }

            // Draw the outer cone circle
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                vertices.Add(outerCircle[i]);
                vertices.Add(outerCircle[next]);
            }

            // Draw inner cone circle (for falloff visualization)
            var innerConeDistance = range * MathF.Cos(innerAngle);
            var innerConeRadius = range * MathF.Sin(innerAngle);

            var innerCircle = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                innerCircle[i] = new Vector3(
                    innerConeDistance,
                    innerConeRadius * MathF.Cos(angle),
                    innerConeRadius * MathF.Sin(angle)
                );
            }

            // Draw inner circle
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                vertices.Add(innerCircle[i]);
                vertices.Add(innerCircle[next]);
            }

            // Draw arcs that show the spherical range boundary cutting off the cone
            // Draw 4 arcs in the cardinal directions from the cone edge curving to the forward point
            for (var arcIndex = 0; arcIndex < 4; arcIndex++)
            {
                var i = arcIndex * segments / 4;
                var theta = 2.0f * MathF.PI * i / segments;

                // Draw an arc along the sphere surface connecting these points
                const int arcSteps = 6;
                for (var step = 0; step < arcSteps; step++)
                {
                    // Interpolate along the sphere surface
                    var t1 = (float) step / arcSteps;
                    var t2 = (float) (step + 1) / arcSteps;

                    // Spherical interpolation from start to end
                    var phi1 = MathF.Acos(coneEndDistance / range) * (1 - t1); // Angle from forward axis
                    var phi2 = MathF.Acos(coneEndDistance / range) * (1 - t2);

                    var p1 = new Vector3(
                        range * MathF.Cos(phi1),
                        range * MathF.Sin(phi1) * MathF.Cos(theta),
                        range * MathF.Sin(phi1) * MathF.Sin(theta)
                    );

                    var p2 = new Vector3(
                        range * MathF.Cos(phi2),
                        range * MathF.Sin(phi2) * MathF.Cos(theta),
                        range * MathF.Sin(phi2) * MathF.Sin(theta)
                    );

                    vertices.Add(p1);
                    vertices.Add(p2);
                }
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
