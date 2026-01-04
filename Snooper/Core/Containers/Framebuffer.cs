using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;

namespace Snooper.Core.Containers;

public abstract class Framebuffer : HandledObject, IBind, IResizable, IMemoryDetailsProvider
{
    public abstract int Width { get; }
    public abstract int Height { get; }

    public GetPName PName => GetPName.FramebufferBinding;
    public int PreviousHandle { get; private set; }

    public override void Generate()
    {
        GL.CreateFramebuffers(1, out uint handle);
        Handle = handle;
    }

    public void Bind()
    {
        PreviousHandle = GL.GetInteger(PName);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
        GL.Viewport(0, 0, Width, Height);
    }

    public void Unbind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, PreviousHandle);
    }

    public abstract void Bind(uint unit);
    public abstract void Resize(int newWidth, int newHeight);
    public abstract Texture[] GetTextures();
    public abstract IEnumerable<MemoryDetail> GetMemoryDetails();

    protected void CheckStatus()
    {
        var status = GL.CheckNamedFramebufferStatus(Handle, FramebufferTarget.Framebuffer);
        if (status != FramebufferStatus.FramebufferComplete)
        {
            throw new Exception($"Framebuffer failed to bind with error: {GL.GetProgramInfoLog((int)Handle)}");
        }
    }

    public override void Dispose()
    {
        GL.DeleteFramebuffer(Handle);
    }
}
