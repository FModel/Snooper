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
    
    public Vector2 ScaleBias;
    public Texture2D Heightmap;
    
    public LandscapeMeshComponent(ULandscapeComponent component) : base(new Geometry(component))
    {
        _component = component;
    }

    public override void Generate(IndirectResources<Vector2> resources)
    {
        base.Generate(resources);

        ScaleBias = new Vector2(_component.HeightmapScaleBias.Z, _component.HeightmapScaleBias.W);
        Heightmap = new Texture2D(_component.GetHeightmap());
    }

    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; } = [new(0, 0, 4)];

    private readonly struct Geometry : TPrimitiveData<Vector2>
    {
        public Vector2[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(ULandscapeComponent component)
        {
            // TODO: generate more quads based on the hardware tesselation limit
            var sizeQuads = component.ComponentSizeQuads;

            Vertices =
            [
                new Vector2(0, 0),
                new Vector2(sizeQuads, 0),
                new Vector2(0, sizeQuads),
                new Vector2(sizeQuads, sizeQuads)
            ];
            
            Indices = [0, 1, 2, 3];
        }
    }
}
