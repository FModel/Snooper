using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : TPrimitiveComponent<Vector2>
{
    private readonly ULandscapeComponent _component;
    
    public readonly int SizeQuads;
    public Texture2D Heightmap;
    public Vector2[] Scales;
    
    public LandscapeMeshComponent(ULandscapeComponent component) : base(new Geometry(component.ComponentSizeQuads))
    {
        _component = component;
        
        SizeQuads = _component.ComponentSizeQuads;
    }

    public override void Generate(IndirectResources<Vector2> resources)
    {
        if (_component.GetHeightmap() is not { } heightmap)
        {
            throw new InvalidOperationException("Landscape component does not have a valid heightmap.");
        }
        
        base.Generate(resources);

        Heightmap = new Texture2D(heightmap);
        
        const int quadCount = Settings.TessellationQuadCount;
        var componentScaleBias = new Vector2(_component.HeightmapScaleBias.Z, _component.HeightmapScaleBias.W);

        Scales = new Vector2[Settings.TessellationIndicesPerQuad / 4];
        for (var x = 0; x < quadCount; x++)
        {
            for (var y = 0; y < quadCount; y++)
            {
                var patchUv = new Vector2(x, y);
                Scales[x * quadCount + y] = patchUv * Settings.TessellationScaleFactor + componentScaleBias;
            }
        }
    }

    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; } = [new(0, 0, Settings.TessellationIndicesPerQuad)];

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
