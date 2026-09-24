using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture2DArray(int width, int height, int depth,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte,
    string? name = null)
    : Texture(width, height, TextureTarget.Texture2DArray, internalFormat, format, type, name: name)
{
    public int Depth { get; } = depth;

    public override void Prepare()
    {
        throw new NotImplementedException();
    }

    protected sealed override void SetStorage()
    {
        GL.TextureStorage3D(Handle, MipCount, FormatInfo.InternalFormat, Width, Height, Depth);
    }

    protected sealed override void SetPixels<T8>(int mip, int width, int height, T8[] pixels)
    {
        switch (FormatInfo)
        {
            case TextureFormatInfo info:
                GL.TextureSubImage3D(Handle, mip, 0, 0, 0, width, height, Depth, info.Format, info.Type, pixels);
                break;
            case CompressedTextureFormatInfo compressed:
                GL.CompressedTextureSubImage3D(Handle, mip, 0, 0, 0, width, height, Depth, (PixelFormat)compressed.InternalFormat, pixels.Length, pixels);
                break;
            default:
                throw new NotSupportedException("Unknown texture format info.");
        }
    }

    public override long Allocated => FormatInfo.GetMemorySize(Width, Height, Depth);
}
