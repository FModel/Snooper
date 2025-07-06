using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture2D(
    int width,
    int height,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte)
    : Texture(width, height, TextureTarget.Texture2D, internalFormat, format, type)
{
    public override GetPName Name => GetPName.TextureBinding2D;

    private readonly UTexture2D? _owner;

    public Texture2D(UTexture2D texture) : this(texture.PlatformData.SizeX, texture.PlatformData.SizeY, GetInternalFormat(texture))
    {
        _owner = texture;
    }

    public override void Generate()
    {
        base.Generate();
        if (_owner is null) return;
        
        Bind();
        
        var bitmap = _owner.Decode();
        GL.TexImage2D(Target, 0, InternalFormat, Width, Height, 0, Format, Type, bitmap.Data);
        
        if (_owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap)
        {
            GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
        }
        else
        {
            GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, 8);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }
    }
    
    private static PixelInternalFormat GetInternalFormat(UTexture2D texture)
    {
        return texture.Format switch
        {
            EPixelFormat.PF_B8G8R8A8 => PixelInternalFormat.Rgba8,
            _ => PixelInternalFormat.Rgb
        };
    }
}
