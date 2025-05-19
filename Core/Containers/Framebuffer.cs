using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class Framebuffer : HandledObject, IBind
{
    public override void Generate()
    {
        Handle = GL.GenFramebuffer();
    }

    public virtual void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }

    public override void Dispose()
    {
        GL.DeleteFramebuffer(Handle);
    }
}
