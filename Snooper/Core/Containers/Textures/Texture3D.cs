using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture3D(int width, int height, int depth,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte,
    string? name = null)
    : Texture(width, height, TextureTarget.Texture2DArray, internalFormat, format, type, name)
{
    public int Depth { get; } = depth;

    protected sealed override void SetStorage(int levels)
    {
        GL.TextureStorage3D(Handle, levels, FormatInfo.InternalFormat, Width, Height, Depth);
    }

    protected sealed override void SetPixels<T8>(T8[] pixels)
    {
        switch (FormatInfo)
        {
            case TextureFormatInfo info:
                GL.TextureSubImage3D(Handle, 0, 0, 0, 0, Width, Height, Depth, info.Format, info.Type, pixels);
                break;
            case CompressedTextureFormatInfo compressed:
                GL.CompressedTextureSubImage3D(Handle, 0, 0, 0, 0, Width, Height, Depth, (PixelFormat)compressed.InternalFormat, pixels.Length, pixels);
                break;
            default:
                throw new NotSupportedException("Unknown texture format info.");
        }
    }
}
