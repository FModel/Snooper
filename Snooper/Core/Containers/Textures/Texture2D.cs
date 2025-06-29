using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture2D : Texture
{
    public override GetPName Name => GetPName.TextureBinding2D;
    
    public Texture2D(
        int width,
        int height,
        PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
        PixelFormat format = PixelFormat.Rgba,
        PixelType type = PixelType.UnsignedByte)
        : base(width, height, TextureTarget.Texture2D, internalFormat, format, type)
    {
        
    }

    public Texture2D(UTexture2D texture) : this(texture.PlatformData.SizeX, texture.PlatformData.SizeY, PixelInternalFormat.Rgba8)
    {
        Generate();
        Bind();

        var bitmap = texture.Decode();
        GL.TexImage2D(Target, 0, InternalFormat, Width, Height, 0, Format, Type, bitmap.Data);
        GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureBaseLevel, 0);
        GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, 8);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }
}
