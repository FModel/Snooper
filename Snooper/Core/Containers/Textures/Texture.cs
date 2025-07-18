using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(
    int width, int height, TextureTarget target,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte) : HandledObject, IBind, IResizable
{
    public abstract GetPName Name { get; }

    public FGuid Guid { get; protected init; }
    public TextureTarget Target { get; } = target;
    public PixelType Type { get; } = type;
    
    public int PreviousHandle { get; private set; }
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;
    public PixelInternalFormat InternalFormat { get; private set; } = internalFormat;
    public PixelFormat Format { get; private set; } = format;

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

    protected void Resize(CTexture texture)
    {
        InternalFormat = texture.PixelFormat switch
        {
            EPixelFormat.PF_G8 => PixelInternalFormat.R8,
            _ => PixelInternalFormat.Rgba
        };
        Format = texture.PixelFormat switch
        {
            EPixelFormat.PF_G8 => PixelFormat.Red,
            EPixelFormat.PF_B8G8R8A8 => PixelFormat.Bgra,
            _ => PixelFormat.Rgba
        };
        
        Resize(texture.Width, texture.Height, texture.Data);
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
    
    public event Action? TextureReadyForBindless;
    protected virtual void OnTextureReadyForBindless()
    {
        TextureReadyForBindless?.Invoke();
    }

    public IntPtr GetPointer() => Handle;

    public override bool Equals(object? obj) => obj is Texture texture && Guid.Equals(texture.Guid);
    public override int GetHashCode() => Guid.GetHashCode();

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}
