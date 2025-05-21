using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(TextureTarget target) : HandledObject, IBind
{
    protected int Width { get; private set; }
    protected int Height { get; private set; }
    protected TextureTarget Target { get; } = target;

    protected Texture(int width, int height, TextureTarget target) : this(target)
    {
        Width = width;
        Height = height;
    }

    public override void Generate()
    {
        Handle = GL.GenTexture();
    }

    protected void Bind(TextureUnit unit)
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
        GL.TexImage2D(Target, 0, PixelInternalFormat.Rgb, newWidth, newHeight, 0, PixelFormat.Rgb, PixelType.UnsignedByte, 0);
    }

    public IntPtr GetPointer() => Handle;

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}
