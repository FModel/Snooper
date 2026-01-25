using System.Numerics;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Components.Light;

public class LightVisualizationComponent : DebugComponent
{
    public LightVisualizationComponent(LocalLightComponent light) : base(new Vector3(0.0f, 0.0f, 1.0f), name: $"{light.Name} (Visualization)")
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), () => light switch
        {
            SpotLightComponent spotLight => new Geometry(spotLight),
            PointLightComponent pointLight => new Geometry(pointLight),
            RectLightComponent rectLight => new Geometry(rectLight),
            _ => throw new NotSupportedException($"Light type {light.GetType().Name} is not supported for visualization.")
        });
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(SpotLightComponent spotLight)
        {
            var vertices = new List<Vector3>();

            // Get cone parameters
            var range = spotLight.AttenuationRadius;
            var outerAngle = spotLight.OuterConeAngle * MathF.PI / 180.0f;
            var innerAngle = spotLight.InnerConeAngle * MathF.PI / 180.0f;

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
                    var t1 = (float)step / arcSteps;
                    var t2 = (float)(step + 1) / arcSteps;

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

        public Geometry(PointLightComponent pointLight)
        {
            var vertices = new List<Vector3>();

            var range = pointLight.AttenuationRadius;
            var center = Vector3.Zero;

            const int segments = 32; // More segments for smoother circles

            // Draw equator circle on XY plane (horizontal)
            for (var i = 0; i < segments; i++)
            {
                var angle1 = 2.0f * MathF.PI * i / segments;
                var angle2 = 2.0f * MathF.PI * (i + 1) / segments;

                var p1 = new Vector3(
                    center.X + range * MathF.Cos(angle1),
                    center.Y + range * MathF.Sin(angle1),
                    center.Z
                );

                var p2 = new Vector3(
                    center.X + range * MathF.Cos(angle2),
                    center.Y + range * MathF.Sin(angle2),
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
                    center.Y + range * MathF.Cos(angle1),
                    center.Z + range * MathF.Sin(angle1)
                );

                var p2 = new Vector3(
                    center.X,
                    center.Y + range * MathF.Cos(angle2),
                    center.Z + range * MathF.Sin(angle2)
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
                    center.X + range * MathF.Cos(angle1),
                    center.Y,
                    center.Z + range * MathF.Sin(angle1)
                );

                var p2 = new Vector3(
                    center.X + range * MathF.Cos(angle2),
                    center.Y,
                    center.Z + range * MathF.Sin(angle2)
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

        public Geometry(RectLightComponent rectLight)
        {
            var vertices = new List<Vector3>();

            var halfWidth = rectLight.Width / 2.0f;
            var halfHeight = rectLight.Height / 2.0f;
            var range = rectLight.AttenuationRadius;

            // X = forward (direction), Y = height (vertical), Z = width (horizontal)
            var topLeft = new Vector3(0, halfHeight, -halfWidth);
            var topRight = new Vector3(0, halfHeight, halfWidth);
            var bottomRight = new Vector3(0, -halfHeight, halfWidth);
            var bottomLeft = new Vector3(0, -halfHeight, -halfWidth);

            // Draw the rectangle outline (source area)
            vertices.Add(topLeft);
            vertices.Add(topRight);

            vertices.Add(topRight);
            vertices.Add(bottomRight);

            vertices.Add(bottomRight);
            vertices.Add(bottomLeft);

            vertices.Add(bottomLeft);
            vertices.Add(topLeft);

            // Draw projection based on barn door or cone angle
            if (rectLight is { BarnDoorAngle: > 0, BarnDoorLength: > 0 })
            {
                // Barn door: Creates a trapezoid-like projection from the rect light
                // The barn door angle is the angle of the barn door from the perpendicular (90 degrees = fully closed)
                // At 0 degrees, barn door is fully open (no narrowing)
                // At 90 degrees, barn door is fully closed (maximum narrowing)

                var barnAngleRad = rectLight.BarnDoorAngle * MathF.PI / 180.0f;
                var barnLength = rectLight.BarnDoorLength;

                // The barn door angle is measured from the perpendicular
                // So we need to use (90 - barnAngle) to get the spreading angle
                var spreadAngleRad = (MathF.PI / 2.0f) - barnAngleRad; // 90 degrees - barn angle

                // If spread angle is negative or zero, the barn door is closed or closing
                // The narrowing is based on how far the barn door extends at the given angle
                float barnHalfWidth, barnHalfHeight;

                if (spreadAngleRad <= 0)
                {
                    // Barn door is at 90 degrees or more - fully closed
                    barnHalfWidth = 0;
                    barnHalfHeight = 0;
                }
                else
                {
                    // Calculate how much the beam narrows
                    var narrowing = barnLength * MathF.Tan(spreadAngleRad);
                    barnHalfWidth = MathF.Max(0, halfWidth - narrowing);
                    barnHalfHeight = MathF.Max(0, halfHeight - narrowing);
                }

                var barnTopLeft = new Vector3(barnLength, barnHalfHeight, -barnHalfWidth);
                var barnTopRight = new Vector3(barnLength, barnHalfHeight, barnHalfWidth);
                var barnBottomRight = new Vector3(barnLength, -barnHalfHeight, barnHalfWidth);
                var barnBottomLeft = new Vector3(barnLength, -barnHalfHeight, -barnHalfWidth);

                // Draw barn door rectangle
                vertices.Add(barnTopLeft);
                vertices.Add(barnTopRight);

                vertices.Add(barnTopRight);
                vertices.Add(barnBottomRight);

                vertices.Add(barnBottomRight);
                vertices.Add(barnBottomLeft);

                vertices.Add(barnBottomLeft);
                vertices.Add(barnTopLeft);

                // Draw lines from source corners to barn door corners
                vertices.Add(topLeft);
                vertices.Add(barnTopLeft);

                vertices.Add(topRight);
                vertices.Add(barnTopRight);

                vertices.Add(bottomRight);
                vertices.Add(barnBottomRight);

                vertices.Add(bottomLeft);
                vertices.Add(barnBottomLeft);

                // Continue to attenuation range with the barn door size
                if (barnLength < range)
                {
                    var projTopLeft = new Vector3(range, barnHalfHeight, -barnHalfWidth);
                    var projTopRight = new Vector3(range, barnHalfHeight, barnHalfWidth);
                    var projBottomRight = new Vector3(range, -barnHalfHeight, barnHalfWidth);
                    var projBottomLeft = new Vector3(range, -barnHalfHeight, -barnHalfWidth);

                    // Draw projection rectangle
                    vertices.Add(projTopLeft);
                    vertices.Add(projTopRight);

                    vertices.Add(projTopRight);
                    vertices.Add(projBottomRight);

                    vertices.Add(projBottomRight);
                    vertices.Add(projBottomLeft);

                    vertices.Add(projBottomLeft);
                    vertices.Add(projTopLeft);

                    // Connect barn door to projection
                    vertices.Add(barnTopLeft);
                    vertices.Add(projTopLeft);

                    vertices.Add(barnTopRight);
                    vertices.Add(projTopRight);

                    vertices.Add(barnBottomRight);
                    vertices.Add(projBottomRight);

                    vertices.Add(barnBottomLeft);
                    vertices.Add(projBottomLeft);
                }
            }
            else if (rectLight.LightFunctionConeAngle > 0)
            {
                // Light function cone angle: Creates a perspective projection (cone-like expansion)
                var coneAngleRad = rectLight.LightFunctionConeAngle * MathF.PI / 180.0f;

                var tanAngle = MathF.Tan(coneAngleRad);
                var expandWidth = range * tanAngle;
                var expandHeight = range * tanAngle;

                var projTopLeft = new Vector3(range, halfHeight + expandHeight, -(halfWidth + expandWidth));
                var projTopRight = new Vector3(range, halfHeight + expandHeight, halfWidth + expandWidth);
                var projBottomRight = new Vector3(range, -(halfHeight + expandHeight), halfWidth + expandWidth);
                var projBottomLeft = new Vector3(range, -(halfHeight + expandHeight), -(halfWidth + expandWidth));

                // Draw projection rectangle
                vertices.Add(projTopLeft);
                vertices.Add(projTopRight);

                vertices.Add(projTopRight);
                vertices.Add(projBottomRight);

                vertices.Add(projBottomRight);
                vertices.Add(projBottomLeft);

                vertices.Add(projBottomLeft);
                vertices.Add(projTopLeft);

                // Draw lines from source corners to projection corners
                vertices.Add(topLeft);
                vertices.Add(projTopLeft);

                vertices.Add(topRight);
                vertices.Add(projTopRight);

                vertices.Add(bottomRight);
                vertices.Add(projBottomRight);

                vertices.Add(bottomLeft);
                vertices.Add(projBottomLeft);
            }
            else
            {
                // No barn door or cone angle - orthographic projection (no spreading)
                // Draw a simple box showing the light extends to 'range' distance
                var projTopLeft = new Vector3(range, halfHeight, -halfWidth);
                var projTopRight = new Vector3(range, halfHeight, halfWidth);
                var projBottomRight = new Vector3(range, -halfHeight, halfWidth);
                var projBottomLeft = new Vector3(range, -halfHeight, -halfWidth);

                // Draw projection rectangle at range distance
                vertices.Add(projTopLeft);
                vertices.Add(projTopRight);

                vertices.Add(projTopRight);
                vertices.Add(projBottomRight);

                vertices.Add(projBottomRight);
                vertices.Add(projBottomLeft);

                vertices.Add(projBottomLeft);
                vertices.Add(projTopLeft);

                // Draw lines from source corners to projection corners
                vertices.Add(topLeft);
                vertices.Add(projTopLeft);

                vertices.Add(topRight);
                vertices.Add(projTopRight);

                vertices.Add(bottomRight);
                vertices.Add(projBottomRight);

                vertices.Add(bottomLeft);
                vertices.Add(projBottomLeft);
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
