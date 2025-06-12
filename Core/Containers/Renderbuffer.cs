using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class Renderbuffer(int width, int height, RenderbufferStorage storage, bool multisampled) : HandledObject, IBind, IResizable
{
    private int _width = width;
    private int _height = height;

    public GetPName Name => GetPName.RenderbufferBinding;
    public int PreviousHandle { get; private set; }

    public override void Generate()
    {
        Handle = GL.GenRenderbuffer();
    }

    public void Bind()
    {
        PreviousHandle = GL.GetInteger(Name);
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Handle);
    }

    public void Unbind()
    {
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, PreviousHandle);
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
