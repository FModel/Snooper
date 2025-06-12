using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;

namespace Snooper.Core.Containers;

public abstract class Framebuffer : HandledObject, IBind, IResizable
{
    public abstract int Width { get; }
    public abstract int Height { get; }

    public GetPName Name => GetPName.FramebufferBinding;
    public int PreviousHandle { get; private set; }

    public override void Generate()
    {
        Handle = GL.GenFramebuffer();
    }

    public virtual void Bind()
    {
        PreviousHandle = GL.GetInteger(Name);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }

    public void Unbind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, PreviousHandle);
    }

    public abstract void Bind(TextureUnit unit);
    public abstract void Render(Action<ShaderProgram>? callback = null);
    public abstract void Resize(int newWidth, int newHeight);
    public abstract IntPtr GetPointer();

    protected void CheckStatus()
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Framebuffer failed to bind with error: {GL.GetProgramInfoLog(Handle)}");
        }
    }

    public override void Dispose()
    {
        GL.DeleteFramebuffer(Handle);
    }
}
