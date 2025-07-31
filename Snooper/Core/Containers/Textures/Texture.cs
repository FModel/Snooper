using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Extensions;

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
    
    public int PreviousHandle { get; private set; }
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;
    public ITextureFormatInfo FormatInfo { get; private set; } = new TextureFormatInfo(internalFormat, format, type);

    public int[] SwizzleMask { get; internal set; } =
    [
        (int) PixelFormat.Red,
        (int) PixelFormat.Green,
        (int) PixelFormat.Blue,
        (int) PixelFormat.Alpha
    ];

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

    protected void Resize(EPixelFormat pixel, FTexture2DMipMap mip)
    {
        FormatInfo = pixel.GetTextureFormat();
        Resize(mip.SizeX, mip.SizeY, mip.BulkData.Data);
        Log.Debug("Texture {Guid} of format {Format} uploaded to GPU with size {Width}x{Height}.", Guid, pixel, Width, Height);
    }
    public void Resize(int newWidth, int newHeight) => Resize<nint>(newWidth, newHeight, []);
    public void Resize<T8>(int newWidth, int newHeight, T8[] pixels) where T8 : unmanaged
    {
        Width = newWidth;
        Height = newHeight;

        Bind();
        switch (Target)
        {
            case TextureTarget.Texture2D when FormatInfo is TextureFormatInfo info:
                GL.TexImage2D(Target, 0, info.InternalFormat, newWidth, newHeight, 0, info.Format, info.Type, pixels);
                break;
            case TextureTarget.Texture2D when FormatInfo is CompressedTextureFormatInfo compressed:
                GL.CompressedTexImage2D(Target, 0, compressed.InternalFormat, Width, Height, 0, pixels.Length, pixels);
                break;
            case TextureTarget.Texture2DMultisample when FormatInfo is TextureFormatInfo info:
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, Settings.NumberOfSamples, info.InternalFormat, newWidth, newHeight, true);
                break;
        }
    }
    
    public void Swizzle()
    {
        GL.TexParameter(Target, TextureParameterName.TextureSwizzleRgba, SwizzleMask);
    }
    
    public event Action? TextureReadyForBindless;
    protected void OnTextureReadyForBindless()
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
