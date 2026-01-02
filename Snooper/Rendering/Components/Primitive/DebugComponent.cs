using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Engine;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Primitive;

public struct PerMaterialDebugData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public float LineThickness { get; init; }
    public ulong Padding { get; init; }
    public Vector3 LineColor { get; init; }
}

[DefaultActorSystem(typeof(DebugSystem))]
public class DebugComponent : PrimitiveComponent<PerMaterialDebugData>
{
    public DebugComponent(PrimitiveData primitive, CullingBounds bounds, string? name = null) : base(primitive, bounds, null, name)
    {

    }

    public DebugComponent(CullingBounds bounds, Vector3? color = null, float lineThickness = 1.0f, string? name = null) : this(new Geometry(bounds), bounds, name)
    {
        if (color != null)
        {
            Materials[0].MaterialDataContainer = new MaterialDataContainer(color.Value, lineThickness);
        }
    }

    public DebugComponent(Vector3 center, Vector3 extents, Vector3? color = null, float lineThickness = 1.0f, string? name = null) : this(new Geometry(center, extents), new CullingBounds(center, extents), name)
    {
        if (color != null)
        {
            Materials[0].MaterialDataContainer = new MaterialDataContainer(color.Value, lineThickness);
        }
    }

    protected DebugComponent(UShapeComponent component) : base(component)
    {

    }

    protected DebugComponent(UPrimitiveComponent component) : base(component)
    {

    }

    protected class MaterialDataContainer(Vector3 color, float lineThickness = 1.0f) : IMaterialDataContainer
    {
        public string Name => Settings.NoName;
        public bool HasTextures => false;
        public bool IsTranslucent => false;
        public Dictionary<string, Texture> GetTextures() => throw new NotImplementedException();
        public void SetBindlessTexture(string key, BindlessTexture bindless) => throw new NotImplementedException();

        public void FinalizeGpuData()
        {
            if (Raw is not null)
                throw new InvalidOperationException("GPU data has already been finalized and sent.");

            Raw = new PerMaterialDebugData
            {
                IsReady = true,
                LineColor = color,
                LineThickness = lineThickness,
            };
        }

        public IPerMaterialData? Raw { get; private set; }

        public void DrawControls()
        {

        }

        public void Dispose()
        {
            Raw = null;
        }
    }

    protected class Geometry : PrimitiveData
    {
        public Geometry(CullingBounds bounds) : this(bounds.Center, bounds.Extents)
        {

        }

        public Geometry(Vector3 center, Vector3 extents)
        {
            BuildBox(center, extents);
        }

        public Geometry(Vector3 center, float sphereRadius)
        {
            BuildSphere(center, new Vector3(sphereRadius));
        }

        public Geometry(float sphereRadius) : this(Vector3.Zero, sphereRadius)
        {

        }

        public Geometry(float radius, float halfHeight)
        {
            BuildCapsule(Vector3.Zero, radius, halfHeight);
        }

        /// <summary>
        /// blame claude if it breaks
        /// </summary>
        public Geometry(UModel brush)
        {
            // Extract points (vertices) from the brush
            var points = brush.Points;
            if (points.Length == 0)
                return;

            // Extract nodes to get triangle information
            var nodes = brush.Nodes;
            if (nodes.Length == 0)
                return;

            // Extract vertex pool
            var verts = brush.Verts;
            if (verts.Length == 0)
                return;

            var vertices = new List<Vector3>();
            var indices = new List<uint>();

            // Process each node separately as independent triangle fans
            foreach (var node in nodes)
            {
                // Skip nodes with less than 3 vertices (can't form a triangle)
                if (node.NumVertices < 3)
                    continue;

                var vertPoolIndex = node.iVertPool;

                // Validate vertex pool index
                if (vertPoolIndex < 0 || vertPoolIndex + node.NumVertices > verts.Length)
                    continue;

                // First, validate all vertices for this node to ensure we can add them all
                bool allVerticesValid = true;
                for (int i = 0; i < node.NumVertices; i++)
                {
                    var vert = verts[vertPoolIndex + i];
                    if (vert.pVertex < 0 || vert.pVertex >= points.Length)
                    {
                        allVerticesValid = false;
                        break;
                    }
                }

                // Skip this node if any vertex is invalid
                if (!allVerticesValid)
                    continue;

                // Get the base index for this node's vertices in our output array
                var baseVertexIndex = (uint)vertices.Count;

                // Add all vertices for this node
                for (int i = 0; i < node.NumVertices; i++)
                {
                    var vert = verts[vertPoolIndex + i];
                    var point = points[vert.pVertex] * Settings.GlobalScale;
                    vertices.Add(new Vector3(point.X, point.Z, point.Y));
                }

                // Create line segments for the polygon edges (perimeter only)
                // Each edge connects consecutive vertices, forming a closed loop
                for (int i = 0; i < node.NumVertices; i++)
                {
                    int nextIndex = (i + 1) % node.NumVertices; // Wrap around to close the loop

                    indices.Add(baseVertexIndex + (uint)i);
                    indices.Add(baseVertexIndex + (uint)nextIndex);
                }
            }

            Vertices = vertices.ToArray();
            Indices = indices.ToArray();
        }

        private void BuildSphere(Vector3 center, Vector3 extents)
        {
            var radius = MathF.Max(MathF.Max(extents.X, extents.Y), extents.Z);

            const int latSegments = 8; // Number of horizontal divisions
            const int lonSegments = 12; // Number of vertical divisions

            var vertices = new List<Vector3>();

            // Generate sphere vertices in a grid pattern
            var spherePoints = new Vector3[latSegments + 1, lonSegments];

            for (var lat = 0; lat <= latSegments; lat++)
            {
                var theta = MathF.PI * lat / latSegments; // 0 to PI (top to bottom)
                var sinTheta = MathF.Sin(theta);
                var cosTheta = MathF.Cos(theta);

                for (var lon = 0; lon < lonSegments; lon++)
                {
                    var phi = 2.0f * MathF.PI * lon / lonSegments; // 0 to 2PI (around)
                    var sinPhi = MathF.Sin(phi);
                    var cosPhi = MathF.Cos(phi);

                    // Sphere coordinates
                    var x = cosPhi * sinTheta;
                    var y = sinPhi * sinTheta;
                    var z = cosTheta;

                    spherePoints[lat, lon] = new Vector3(
                        center.X + radius * x,
                        center.Y + radius * y,
                        center.Z + radius * z
                    );
                }
            }

            // Create line segments for each rectangular face
            // Horizontal lines (latitude lines)
            for (var lat = 0; lat <= latSegments; lat++)
            {
                for (var lon = 0; lon < lonSegments; lon++)
                {
                    var nextLon = (lon + 1) % lonSegments;
                    vertices.Add(spherePoints[lat, lon]);
                    vertices.Add(spherePoints[lat, nextLon]);
                }
            }

            // Vertical lines (longitude lines)
            for (var lon = 0; lon < lonSegments; lon++)
            {
                for (var lat = 0; lat < latSegments; lat++)
                {
                    vertices.Add(spherePoints[lat, lon]);
                    vertices.Add(spherePoints[lat + 1, lon]);
                }
            }

            Vertices = vertices.ToArray();

            Indices = new uint[Vertices.Length];
            for (uint i = 0; i < Indices.Length; i++)
            {
                Indices[i] = i;
            }
        }

        private void BuildBox(Vector3 center, Vector3 extents)
        {
            var c0 = new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z - extents.Z);
            var c1 = new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z - extents.Z);
            var c2 = new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z - extents.Z);
            var c3 = new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z - extents.Z);
            var c4 = new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z + extents.Z);
            var c5 = new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z + extents.Z);
            var c6 = new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z + extents.Z);
            var c7 = new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z + extents.Z);

            Vertices =
            [
                c0, c1,
                c0, c3,
                c0, c4,
                c1, c2,
                c1, c5,
                c2, c6,
                c3, c2,
                c3, c7,
                c4, c5,
                c4, c7,
                c5, c6,
                c7, c6
            ];

            Indices =
            [
                0, 1,
                2, 3,
                4, 5,
                6, 7,
                8, 9,
                10, 11,
                12, 13,
                14, 15,
                16, 17,
                18, 19,
                20, 21,
                22, 23
            ];
        }

        private void BuildCapsule(Vector3 center, float radius, float halfHeight)
        {
            const int segments = 12; // Number of segments around the capsule
            const int capSegments = 4; // Number of segments for each hemisphere cap

            var vertices = new List<Vector3>();

            // In Unreal, halfHeight includes the hemisphere caps
            // So the cylindrical part extends from (halfHeight - radius) to -(halfHeight - radius)
            var cylinderHalfHeight = halfHeight - radius;

            // Calculate the top and bottom centers of the cylindrical part (Y is up)
            var topCenter = center with { Y = center.Y + cylinderHalfHeight };
            var bottomCenter = center with { Y = center.Y - cylinderHalfHeight };

            // Generate points around the circumference at different heights
            var cylinderRings = new Vector3[2, segments];

            // Bottom ring of cylinder (XZ plane)
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                var x = MathF.Cos(angle) * radius;
                var z = MathF.Sin(angle) * radius;
                cylinderRings[0, i] = new Vector3(bottomCenter.X + x, bottomCenter.Y, bottomCenter.Z + z);
            }

            // Top ring of cylinder (XZ plane)
            for (var i = 0; i < segments; i++)
            {
                var angle = 2.0f * MathF.PI * i / segments;
                var x = MathF.Cos(angle) * radius;
                var z = MathF.Sin(angle) * radius;
                cylinderRings[1, i] = new Vector3(topCenter.X + x, topCenter.Y, topCenter.Z + z);
            }

            // Draw vertical lines connecting bottom and top rings
            for (var i = 0; i < segments; i++)
            {
                vertices.Add(cylinderRings[0, i]);
                vertices.Add(cylinderRings[1, i]);
            }

            // Draw circumference rings
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                vertices.Add(cylinderRings[0, i]);
                vertices.Add(cylinderRings[0, next]);

                vertices.Add(cylinderRings[1, i]);
                vertices.Add(cylinderRings[1, next]);
            }

            // Generate bottom hemisphere cap
            var bottomCapRings = new Vector3[capSegments + 1, segments];
            for (var ring = 0; ring <= capSegments; ring++)
            {
                var phi = MathF.PI / 2 + (MathF.PI / 2) * ring / capSegments; // PI/2 to PI
                var ringRadius = MathF.Sin(phi) * radius;
                var y = MathF.Cos(phi) * radius;

                for (var i = 0; i < segments; i++)
                {
                    var angle = 2.0f * MathF.PI * i / segments;
                    var x = MathF.Cos(angle) * ringRadius;
                    var z = MathF.Sin(angle) * ringRadius;
                    bottomCapRings[ring, i] = new Vector3(bottomCenter.X + x, bottomCenter.Y + y, bottomCenter.Z + z);
                }
            }

            // Draw bottom hemisphere lines
            for (var ring = 0; ring < capSegments; ring++)
            {
                for (var i = 0; i < segments; i++)
                {
                    var next = (i + 1) % segments;
                    // Horizontal lines
                    vertices.Add(bottomCapRings[ring, i]);
                    vertices.Add(bottomCapRings[ring, next]);

                    // Vertical lines
                    vertices.Add(bottomCapRings[ring, i]);
                    vertices.Add(bottomCapRings[ring + 1, i]);
                }
            }

            // Generate top hemisphere cap
            var topCapRings = new Vector3[capSegments + 1, segments];
            for (var ring = 0; ring <= capSegments; ring++)
            {
                var phi = MathF.PI / 2 * ring / capSegments; // 0 to PI/2
                var ringRadius = MathF.Sin(phi) * radius;
                var y = MathF.Cos(phi) * radius;

                for (var i = 0; i < segments; i++)
                {
                    var angle = 2.0f * MathF.PI * i / segments;
                    var x = MathF.Cos(angle) * ringRadius;
                    var z = MathF.Sin(angle) * ringRadius;
                    topCapRings[ring, i] = new Vector3(topCenter.X + x, topCenter.Y + y, topCenter.Z + z);
                }
            }

            // Draw top hemisphere lines
            for (var ring = 0; ring < capSegments; ring++)
            {
                for (var i = 0; i < segments; i++)
                {
                    var next = (i + 1) % segments;
                    // Horizontal lines
                    vertices.Add(topCapRings[ring, i]);
                    vertices.Add(topCapRings[ring, next]);

                    // Vertical lines
                    vertices.Add(topCapRings[ring, i]);
                    vertices.Add(topCapRings[ring + 1, i]);
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
