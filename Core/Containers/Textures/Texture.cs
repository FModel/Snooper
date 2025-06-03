using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(TextureTarget target) : HandledObject, IBind, IResizable
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureTarget Target { get; } = target;

    protected Texture(int width, int height, TextureTarget target) : this(target)
    {
        Width = width;
        Height = height;
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
        if (Handle < 1) throw new Exception("Bind called on an unhandled texture handle");
        GL.BindTexture(Target, Handle);
    }

    public void Resize(int newWidth, int newHeight)
    {
        Width = newWidth;
        Height = newHeight;

        Bind();
        switch (Target)
        {
            case TextureTarget.Texture2D:
                GL.TexImage2D(Target, 0, PixelInternalFormat.Rgb, newWidth, newHeight, 0, PixelFormat.Rgb, PixelType.UnsignedByte, 0);
                break;
            case TextureTarget.Texture2DMultisample:
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, Settings.NumberOfSamples, PixelInternalFormat.Rgb, newWidth, newHeight, true);
                break;
        }
    }

    public IntPtr GetPointer() => Handle;

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}
