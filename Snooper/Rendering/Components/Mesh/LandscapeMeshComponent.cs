using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : PrimitiveComponent
{
    private readonly ULandscapeComponent[] _components;
    
    public Texture[] Heightmaps { get; private set; } = [];
    
    public LandscapeMeshComponent(ALandscapeProxy landscape) : base(new Geometry(landscape))
    {
        // _components = landscape.GetOrDefault<ULandscapeComponent[]>("LandscapeComponents", []);
    }

    public override void Generate(IndirectResources<Vector3> resources)
    {
        base.Generate(resources);

        // Heightmaps = new Texture[_components.Length];
        // for (var i = 0; i < Heightmaps.Length; i++)
        // {
        //     var heightmap = _components[i].GetHeightmap();
        //     Heightmaps[i] = new Texture2D(heightmap);
        // }
    }

    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(ALandscapeProxy landscape)
        {
            var components = landscape.GetOrDefault<ULandscapeComponent[]>("LandscapeComponents", []);
            
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            var sizeQuads = landscape.ComponentSizeQuads;
            
            foreach (var component in components)
            {
                if (sizeQuads == -1) sizeQuads = component.ComponentSizeQuads;
                component.GetComponentExtent(ref minX, ref minY, ref maxX, ref maxY);
            }
            
            var scaleX = (float)(maxX - minX) / sizeQuads;
            var scaleY = (float)(maxY - minY) / sizeQuads;
            var w = sizeQuads + 1;
            var h = sizeQuads + 1;
            
            Vertices = new Vector3[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    Vertices[y * w + x] = new Vector3(x * scaleX, 0, y * scaleY) * Settings.GlobalScale;
                }
            }
            
            Indices = new uint[(w - 1) * (h - 1) * 6];
            var index = 0;
            for (var y = 0; y < h - 1; y++)
            {
                for (var x = 0; x < w - 1; x++)
                {
                    var i = (uint)(y * w + x);

                    Indices[index++] = i;
                    Indices[index++] = i + (uint)w;
                    Indices[index++] = i + (uint)w + 1;

                    Indices[index++] = i;
                    Indices[index++] = i + (uint)w + 1;
                    Indices[index++] = i + 1;
                }
            }
        }
    }
}
