using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public struct PerDrawDebugData : IPerDrawData
{
    public bool IsReady { get; init; }
    public long Padding { get; init; }
    public Vector3 Color { get; init; }
}

[DefaultActorSystem(typeof(DebugSystem))]
public class DebugComponent(IPrimitiveData primitive) : PrimitiveComponent<PerDrawDebugData>(primitive)
{
    public DebugComponent(BoxCullingComponent box, Vector3? color = null) : this(new Geometry(box))
    {
        if (color != null)
        {
            Sections[0].DrawDataContainer = new DrawDataContainer(color.Value);
        }
    }

    public DebugComponent(SphereCullingComponent sphere, Vector3? color = null) : this(new Geometry(sphere))
    {
        if (color != null)
        {
            Sections[0].DrawDataContainer = new DrawDataContainer(color.Value);
        }
    }
    
    private class DrawDataContainer(Vector3 color) : IDrawDataContainer
    {
        public bool HasTextures => false;
        public Dictionary<string, Texture> GetTextures() => throw new NotImplementedException();
        public void SetBindlessTexture(string key, BindlessTexture bindless) => throw new NotImplementedException();

        public void FinalizeGpuData()
        {
            Raw = new PerDrawDebugData
            {
                IsReady = true,
                Color = color,
            };
        }
        
        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            ImGui.ColorButton("Debug Color", new Vector4(color, 1.0f), ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoTooltip);
        }
    }

    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(BoxCullingComponent box)
        {
            var center = box.Center;
            var extents = box.Extents;

            Vertices =
            [
                new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z - extents.Z),
                new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z - extents.Z),
                new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z - extents.Z),
                new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z - extents.Z),
                new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z + extents.Z),
                new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z + extents.Z),
                new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z + extents.Z),
                new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z + extents.Z)
            ];

            Indices =
            [
                0, 1, 2, 0, 2, 3, // Bottom face
                4, 5, 6, 4, 6, 7, // Top face
                0, 1, 5, 0, 5, 4, // Front face
                2, 3, 7, 2, 7, 6, // Back face
                0, 3, 7, 0, 7, 4, // Left face
                1, 2, 6, 1, 6, 5 // Right face
            ];
        }

        public Geometry(SphereCullingComponent sphere)
        {
            var origin = sphere.Origin;
            var center = sphere.Center;
            var radius = sphere.Radius;

            const int segments = 16;

            // Generate vertices for the sphere
            Vertices = new Vector3[segments * segments];
            for (int i = 0; i < segments; i++)
            {
                float theta = MathF.PI * i / (segments - 1);
                for (int j = 0; j < segments; j++)
                {
                    float phi = 2.0f * MathF.PI * j / (segments - 1);
                    float x = radius * MathF.Sin(theta) * MathF.Cos(phi);
                    float y = radius * MathF.Sin(theta) * MathF.Sin(phi);
                    float z = radius * MathF.Cos(theta);
                    Vertices[i * segments + j] = new Vector3(x, z, y) + center;
                }
            }

            // Generate indices for the sphere
            Indices = new uint[(segments - 1) * (segments - 1) * 6];

            int index = 0;
            for (int i = 0; i < segments - 1; i++)
            {
                for (int j = 0; j < segments - 1; j++)
                {
                    int a = i * segments + j;
                    int b = a + segments;
                    int c = a + 1;
                    int d = b + 1;

                    Indices[index++] = (uint)a;
                    Indices[index++] = (uint)b;
                    Indices[index++] = (uint)c;

                    Indices[index++] = (uint)b;
                    Indices[index++] = (uint)d;
                    Indices[index++] = (uint)c;
                }
            }
        }
    }
}
