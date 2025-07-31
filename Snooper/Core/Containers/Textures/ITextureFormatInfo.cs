using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public interface ITextureFormatInfo;

public readonly struct TextureFormatInfo(PixelInternalFormat internalFormat, PixelFormat format, PixelType type) : ITextureFormatInfo
{
    public readonly PixelInternalFormat InternalFormat = internalFormat;
    public readonly PixelFormat Format = format;
    public readonly PixelType Type = type;
}

public readonly struct CompressedTextureFormatInfo(InternalFormat internalFormat) : ITextureFormatInfo
{
    public readonly InternalFormat InternalFormat = internalFormat;
}