using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class Renderbuffer : HandledObject, IBind
{
    public override void Generate()
    {
        Handle = GL.GenRenderbuffer();
    }

    public void Bind()
    {
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Handle);
    }

    public override void Dispose()
    {
        GL.DeleteRenderbuffer(Handle);
    }
}
