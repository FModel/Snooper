using OpenTK.Graphics.OpenGL4;
using Snooper.Extensions;

namespace Snooper.Core.Containers.Textures;

public class ResizableTexture2D(int width, int height,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte,
    string? name = null) : Texture2D(width, height, internalFormat, format, type, name), IResizable, IBind
{
    public GetPName PName => GetPName.TextureBinding2D;
    public int PreviousHandle { get; private set; }
    
    public void Bind()
    {
        PreviousHandle = GL.GetInteger(PName);
        GL.BindTexture(Target, Handle);
    }

    public void Unbind()
    {
        GL.BindTexture(Target, PreviousHandle);
    }
    
    public void Resize(int newWidth, int newHeight)
    {
        Width = newWidth;
        Height = newHeight;

        Bind();
        switch (Target)
        {
            case TextureTarget.Texture2D when FormatInfo is TextureFormatInfo info:
                GL.TexImage2D(Target, 0, info.InternalFormat.ToPixelInternalFormat(), newWidth, newHeight, 0, info.Format, info.Type, 0);
                break;
            case TextureTarget.Texture2D when FormatInfo is CompressedTextureFormatInfo compressed:
                GL.CompressedTexImage2D(Target, 0, compressed.InternalFormat.ToInternalFormat(), Width, Height, 0, 0, 0);
                break;
            case TextureTarget.Texture2DMultisample when FormatInfo is TextureFormatInfo info:
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, Settings.NumberOfSamples, info.InternalFormat.ToPixelInternalFormat(), newWidth, newHeight, true);
                break;
        }
    }
}