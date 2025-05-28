using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class Renderbuffer(int width, int height, RenderbufferStorage storage) : HandledObject, IBind, IResizable
{
    public override void Generate()
    {
        Handle = GL.GenRenderbuffer();
    }

    public void Bind()
    {
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Handle);
    }

    public void Resize(int newWidth, int newHeight)
    {
        width = newWidth;
        height = newHeight;

        Bind();
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, storage, width, height);
    }

    public override void Dispose()
    {
        GL.DeleteRenderbuffer(Handle);
    }
}
