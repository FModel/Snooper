using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public abstract class Texture(TextureTarget target) : Object, IBind
{
    public override void Generate()
    {
        Handle = GL.GenTexture();
    }

    public void Bind()
    {
        GL.BindTexture(target, Handle);
    }

    public void Unbind()
    {
        GL.BindTexture(target, 0);
    }

    public override void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}
