using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public struct PerDrawDebugData : IPerDrawData
{
    public bool IsReady { get; init; }
    public ulong Padding { get; init; }
    public Vector3 LineColor { get; init; }
}

[DefaultActorSystem(typeof(DebugSystem))]
public class DebugComponent(PrimitiveData primitive, CullingBounds bounds, string? name = null) : PrimitiveComponent<PerDrawDebugData>(primitive, bounds, null, name)
{
    public DebugComponent(CullingBounds bounds, Vector3? color = null, string? name = null) : this(new Geometry(bounds), bounds, name)
    {
        if (color != null)
        {
            Materials[0].DrawDataContainer = new DrawDataContainer(color.Value);
        }
    }
    
    private class DrawDataContainer(Vector3 color) : IDrawDataContainer
    {
        public bool HasTextures => false;
        public bool IsTranslucent => false;
        public Dictionary<string, Texture> GetTextures() => throw new NotImplementedException();
        public void SetBindlessTexture(string key, BindlessTexture bindless) => throw new NotImplementedException();

        public void FinalizeGpuData()
        {
            Raw = new PerDrawDebugData
            {
                IsReady = true,
                LineColor = color,
            };
        }
        
        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            
        }

        public void Dispose()
        {
            Raw = null;
        }
    }

    private class Geometry : PrimitiveData
    {
        public Geometry(CullingBounds bounds) : this(bounds.Center, bounds.Extents)
        {
            
        }
        
        public Geometry(CullingBounds bounds, bool isSphere) : this(bounds.Center, bounds.Extents, isSphere)
        {
            
        }

        private Geometry(Vector3 center, Vector3 extents, bool isSphere = false)
        {
            if (isSphere)
            {
                BuildSphere(center, extents);
            }
            else
            {
                BuildBox(center, extents);
            }
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
            
            // Indices are simply sequential since we already paired the vertices
            var indices = new List<uint>();
            for (uint i = 0; i < vertices.Count; i++)
            {
                indices.Add(i);
            }
            Indices = indices.ToArray();
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
                // Corner 0 (---): lines along X, Y, Z axes
                c0, c1,  // X-axis to corner 1
                c0, c3,  // Y-axis to corner 3
                c0, c4,  // Z-axis to corner 4
                
                // Corner 1 (+--): lines along Y, Z axes (X already covered)
                c1, c2,  // Y-axis to corner 2
                c1, c5,  // Z-axis to corner 5
                
                // Corner 2 (+--): lines along Z axis (X, Y already covered)
                c2, c6,  // Z-axis to corner 6
                
                // Corner 3 (-+-): lines along Z axis (X, Y already covered)
                c3, c2,  // X-axis to corner 2
                c3, c7,  // Z-axis to corner 7
                
                // Corner 4 (--+): lines along X, Y axes (Z already covered)
                c4, c5,  // X-axis to corner 5
                c4, c7,  // Y-axis to corner 7
                
                // Corner 5 (+-+): lines along Y axis (X, Z already covered)
                c5, c6,  // Y-axis to corner 6
                
                // Corner 6 (+++): all edges already covered
                
                // Corner 7 (-++): lines along X axis (Y, Z already covered)
                c7, c6   // X-axis to corner 6
            ];

            Indices =
            [
                // Bottom face (4 edges)
                0, 1,    // c0 to c1
                2, 3,    // c0 to c3
                4, 5,    // c1 to c2
                6, 7,    // c3 to c2
                
                // Top face (4 edges)
                8, 9,    // c4 to c5
                10, 11,  // c4 to c7
                12, 13,  // c5 to c6
                14, 15,  // c7 to c6
                
                // Vertical edges (4 edges)
                16, 17,  // c0 to c4
                18, 19,  // c1 to c5
                20, 21,  // c2 to c6
                22, 23   // c3 to c7
            ];
        }
    }
}
