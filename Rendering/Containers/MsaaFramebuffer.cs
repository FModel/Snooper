using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers;

public class MsaaFramebuffer(int originalWidth, int originalHeight) : Framebuffer, IResizable
{
    private readonly PostProcFramebuffer _postProcFramebuffer = new(originalWidth, originalHeight);
    private readonly Texture2DMultisample _color = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(originalWidth, originalHeight);
        
        _depth.Generate();
        _depth.Resize(originalWidth, originalHeight);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _color.Target, _color, 0);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
        
        _postProcFramebuffer.Generate();
    }

    public void RenderPostProcessing()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Handle);
        _postProcFramebuffer.RenderPostProcessing();
    }

    public void Resize(int newWidth, int newHeight)
    {
        _postProcFramebuffer.Resize(newWidth, newHeight);
        _color.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public IntPtr GetPointer() => _postProcFramebuffer.GetPointer();
}
