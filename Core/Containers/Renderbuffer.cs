using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class Renderbuffer(int width, int height, RenderbufferStorage storage, bool multisampled) : HandledObject, IBind, IResizable
{
    private int _width = width;
    private int _height = height;

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
        _width = newWidth;
        _height = newHeight;

        Bind();

        if (multisampled)
        {
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Settings.NumberOfSamples, storage, _width, _height);
        }
        else
        {
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, storage, _width, _height);
        }
    }

    public override void Dispose()
    {
        GL.DeleteRenderbuffer(Handle);
    }
}
