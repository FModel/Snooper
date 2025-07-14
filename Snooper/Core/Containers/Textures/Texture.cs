using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(TextureTarget target) : HandledObject, IBind, IResizable
{
    public abstract GetPName Name { get; }

    public FGuid Guid { get; protected init; }
    public int PreviousHandle { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureTarget Target { get; } = target;
    public PixelInternalFormat InternalFormat { get; }
    public PixelFormat Format { get; }
    public PixelType Type { get; }

    protected Texture(int width, int height, TextureTarget target, PixelInternalFormat internalFormat, PixelFormat format, PixelType type) : this(target)
    {
        Guid = System.Guid.NewGuid();
        Width = width;
        Height = height;
        InternalFormat = internalFormat;
        Format = format;
        Type = type;
    }

    public override void Generate()
    {
        Handle = GL.GenTexture();
    }

    public void Bind(TextureUnit unit)
    {
        GL.ActiveTexture(unit);
        Bind();
    }


    public void Bind()
    {
        PreviousHandle = GL.GetInteger(Name);
        GL.BindTexture(Target, Handle);
    }

    public void Unbind()
    {
        GL.BindTexture(Target, PreviousHandle);
    }

    public void Resize(int newWidth, int newHeight) => Resize<nint>(newWidth, newHeight, []);
    public void Resize<T8>(int newWidth, int newHeight, T8[] pixels) where T8 : unmanaged
    {
        Width = newWidth;
        Height = newHeight;

        Bind();
        switch (Target)
        {
            case TextureTarget.Texture2D:
                GL.TexImage2D(Target, 0, InternalFormat, newWidth, newHeight, 0, Format, Type, pixels);
                break;
            case TextureTarget.Texture2DMultisample:
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, Settings.NumberOfSamples, InternalFormat, newWidth, newHeight, true);
                break;
        }
    }

    public IntPtr GetPointer() => Handle;

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}
