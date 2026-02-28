using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Engine;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Primitive;

public class BrushComponent : DebugComponent
{
    public BrushComponent(UBrushComponent component, UModel brush) : base(component)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(brush.Bounds.GetBox(), () => new Geometry(brush));

        Materials[0].InlineContainer = new MaterialDataContainer(new Vector3(0.75f, 0, 0));
    }

    private class Geometry : DebugGeometry
    {
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
    }
}
