using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public abstract class Framebuffer : HandledObject, IBind, IResizable
{
    public override void Generate()
    {
        Handle = GL.GenFramebuffer();
    }

    public virtual void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }

    protected void CheckStatus()
    {
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Framebuffer failed to bind with error: {GL.GetProgramInfoLog(Handle)}");
        }
    }

    public abstract void Resize(int newWidth, int newHeight);
    public abstract IntPtr GetPointer();

    public override void Dispose()
    {
        GL.DeleteFramebuffer(Handle);
    }
}
