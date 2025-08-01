using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public struct PerDrawLandscapeData : IPerDrawData
{
    public bool IsReady { get; init; }
    public int Padding { get; init; }
    public long Heightmap { get; init; }
    public Vector2 ScaleBias { get; init; }
}

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : PrimitiveComponent<Vector2, PerDrawLandscapeData>
{
    public readonly int SizeQuads;
    public readonly Vector2[] Scales;
    
    public LandscapeMeshComponent(ULandscapeComponent component) : base(new Geometry(component.ComponentSizeQuads), component.CachedLocalBox)
    {
        if (component.GetHeightmap() is not { } heightmap)
        {
            throw new InvalidOperationException("Landscape component does not have a valid heightmap.");
        }

        Sections[0].DrawDataContainer = new DrawDataContainer(new Texture2D(heightmap), new Vector2(component.HeightmapScaleBias.Z, component.HeightmapScaleBias.W));
        
        SizeQuads = component.ComponentSizeQuads + 1;
        Scales = new Vector2[Settings.TessellationQuadCountTotal];
        
        const int quadCount = Settings.TessellationQuadCount;
        for (var x = 0; x < quadCount; x++)
        {
            for (var y = 0; y < quadCount; y++)
            {
                Scales[x * quadCount + y] = new Vector2(x, y);
            }
        }
    }

    private class DrawDataContainer(Texture heightmap, Vector2 scaleBias) : IDrawDataContainer
    {
        private BindlessTexture? _heightmap;
        
        public bool HasTextures => true;

        public Dictionary<string, Texture> GetTextures() => new()
        {
            ["Heightmap"] = heightmap
        };

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            _heightmap = key switch
            {
                "Heightmap" => bindless,
                _ => throw new ArgumentException($"Unknown texture key: {key}")
            };
        }

        public void FinalizeGpuData()
        {
            if (_heightmap is null)
            {
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");
            }
            
            _heightmap.Generate();
            _heightmap.MakeResident();
            
            Raw = new PerDrawLandscapeData
            {
                IsReady = true,
                Heightmap = _heightmap,
                ScaleBias = scaleBias
            };
        }
        
        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            var largest = ImGui.GetContentRegionAvail();
            largest.X -= ImGui.GetScrollX();
            
            ImGui.Image(heightmap.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
        }
    }

    private readonly struct Geometry : TPrimitiveData<Vector2>
    {
        public Vector2[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(int sizeQuads)
        {
            const int quadCount = Settings.TessellationQuadCount;
            var halfSize = sizeQuads / (float)quadCount;

            Vertices = new Vector2[Settings.TessellationIndicesPerQuad];
            Indices = new uint[Vertices.Length];

            for (var i = 0; i < quadCount; i++)
            {
                for (var j = 0; j < quadCount; j++)
                {
                    var index = i * quadCount + j;
                    var xOffset = i * halfSize;
                    var yOffset = j * halfSize;

                    Vertices[index * 4] = new Vector2(xOffset, yOffset);
                    Vertices[index * 4 + 1] = new Vector2(xOffset + halfSize, yOffset);
                    Vertices[index * 4 + 2] = new Vector2(xOffset, yOffset + halfSize);
                    Vertices[index * 4 + 3] = new Vector2(xOffset + halfSize, yOffset + halfSize);

                    Indices[index * 4] = (uint)(index * 4);
                    Indices[index * 4 + 1] = (uint)(index * 4 + 1);
                    Indices[index * 4 + 2] = (uint)(index * 4 + 2);
                    Indices[index * 4 + 3] = (uint)(index * 4 + 3);
                }
            }
        }
    }
}
