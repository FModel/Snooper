using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(TextureTarget target) : HandledObject, IBind
{
    protected int Width { get; }
    protected int Height { get; }
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

    public void Bind(TextureUnit unit)
    {
        GL.ActiveTexture(unit);
        Bind();
    }

    public void Bind()
    {
        GL.BindTexture(Target, Handle);
    }

    public IntPtr GetPointer() => Handle;

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}
