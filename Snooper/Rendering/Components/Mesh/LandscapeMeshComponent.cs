using System.Numerics;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(LandscapeSystem))]
public class LandscapeMeshComponent : PrimitiveComponent
{
    public LandscapeMeshComponent(ULandscapeComponent component) : base(new Geometry(component))
    {
        
    }

    private readonly struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(ULandscapeComponent component)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            component.GetComponentExtent(ref minX, ref minY, ref maxX, ref maxY);
            
            var sizeQuads = component.ComponentSizeQuads;
            var scaleX = (float)(maxX - minX) / sizeQuads;
            var scaleY = (float)(maxY - minY) / sizeQuads;
            var w = sizeQuads + 1;
            var h = sizeQuads + 1;
            
            var heightmap = component.GetHeightmap().Decode();
            var middle = w * h * 2;
            
            Vertices = new Vector3[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var localX = (int)(heightmap.Width * component.HeightmapScaleBias.Z) + x;
                    var localY = (int)(heightmap.Height * component.HeightmapScaleBias.W) + y;
                    var pixelIndex = (localY * heightmap.Height + localX) * 4;
                    
                    var red = heightmap.Data[pixelIndex + 2];
                    var green = heightmap.Data[pixelIndex + 1];
                    var height = ((red << 8) + green - middle) / (float)h;
                    
                    Vertices[y * w + x] = new Vector3(x * scaleX, height, y * scaleY) * Settings.GlobalScale;
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
