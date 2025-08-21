using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public struct PerDrawDebugData : IPerDrawData
{
    public bool IsReady { get; init; }
    public ulong Padding { get; init; }
    public Vector3 Color { get; init; }
}

[DefaultActorSystem(typeof(DebugSystem))]
public class DebugComponent(PrimitiveData primitive, CullingBounds bounds) : PrimitiveComponent<PerDrawDebugData>(primitive, bounds)
{
    public DebugComponent(CullingBounds bounds, Vector3? color = null) : this(new Geometry(bounds), bounds)
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
                Color = color,
            };
        }
        
        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            ImGui.ColorButton("Debug Color", new Vector4(color, 1.0f), ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoTooltip);
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

        private Geometry(Vector3 center, Vector3 extents)
        {
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
    }
}
