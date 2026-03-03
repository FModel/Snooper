using System.Numerics;
using Snooper.Rendering.Components.Light;

namespace Snooper.Rendering.Components.Visualization;

public class RectLightComponentVisualization(RectLightComponent light) : LightComponentVisualization(light, Settings.RectLight, () => new Geometry(light))
{
    private class Geometry : DebugGeometry
    {
        public Geometry(RectLightComponent light)
        {
            var vertices = new List<Vector3>();

            var halfWidth = light.Width / 2.0f;
            var halfHeight = light.Height / 2.0f;
            var range = light.AttenuationRadius;

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
            if (light is { BarnDoorAngle: > 0, BarnDoorLength: > 0 })
            {
                // Barn door: Creates a trapezoid-like projection from the rect light
                // The barn door angle is the angle of the barn door from the perpendicular (90 degrees = fully closed)
                // At 0 degrees, barn door is fully open (no narrowing)
                // At 90 degrees, barn door is fully closed (maximum narrowing)

                var barnAngleRad = light.BarnDoorAngle * MathF.PI / 180.0f;
                var barnLength = light.BarnDoorLength;

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
            else if (light.LightFunctionConeAngle > 0)
            {
                // Light function cone angle: Creates a perspective projection (cone-like expansion)
                var coneAngleRad = light.LightFunctionConeAngle * MathF.PI / 180.0f;

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
