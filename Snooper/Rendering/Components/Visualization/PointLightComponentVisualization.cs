using System.Numerics;
using Snooper.Rendering.Components.Light;

namespace Snooper.Rendering.Components.Visualization;

public class PointLightComponentVisualization(PointLightComponent light) : LightComponentVisualization(light, Settings.PointLight, () => new Geometry(light))
{
    private class Geometry : DebugGeometry
    {
        public Geometry(PointLightComponent light)
        {
            var vertices = new List<Vector3>();

            var range = light.AttenuationRadius;
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
    }
}
