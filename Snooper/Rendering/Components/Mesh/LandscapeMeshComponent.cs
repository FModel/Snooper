using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public struct PerDrawLandscapeData : IPerDrawData
{
    public long Heightmap { get; set; }
    public Vector2 ScaleBias { get; set; }
    
    public void SetDefault()
    {
        var texture = new ColorTexture(FColor.Gray);
        texture.Generate();
        
        var heightmap = new BindlessTexture(texture);
        heightmap.Generate();
        heightmap.MakeResident();
        
        Heightmap = heightmap;
        ScaleBias = Vector2.Zero;
    }
}

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : TPrimitiveComponent<Vector2, PerInstanceData, PerDrawLandscapeData>
{
    public readonly int SizeQuads;
    public readonly Texture2D Heightmap;
    public readonly Vector2 ScaleBias;
    public readonly Vector2[] Scales;
    
    public LandscapeMeshComponent(ULandscapeComponent component) : base(new Geometry(component.ComponentSizeQuads))
    {
        if (component.GetHeightmap() is not { } heightmap)
        {
            throw new InvalidOperationException("Landscape component does not have a valid heightmap.");
        }
        
        SizeQuads = component.ComponentSizeQuads + 1;
        Heightmap = new Texture2D(heightmap);

        Scales = new Vector2[Settings.TessellationQuadCountTotal];
        ScaleBias = new Vector2(component.HeightmapScaleBias.Z, component.HeightmapScaleBias.W);
        
        const int quadCount = Settings.TessellationQuadCount;
        for (var x = 0; x < quadCount; x++)
        {
            for (var y = 0; y < quadCount; y++)
            {
                Scales[x * quadCount + y] = new Vector2(x, y);
            }
        }
    }

    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; } = [new(0, 0, Settings.TessellationIndicesPerQuad)];

    public override void Generate(IndirectResources<Vector2, PerInstanceData, PerDrawLandscapeData> resources)
    {
        base.Generate(resources);
        
        Heightmap.OnTextureReady += () =>
        {
            Heightmap.MakeBindless();
            resources.UpdateDrawData(DrawMetadata.DrawIds[0], new PerDrawLandscapeData
            {
                Heightmap = Heightmap.Bindless,
                ScaleBias = ScaleBias
            });
        };
        Heightmap.Generate();
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
