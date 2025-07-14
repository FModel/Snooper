using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ForwardFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    public override void Generate()
    {
        base.Generate();
        
        _depth.Generate();
        _depth.Resize(Width, Height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }
}
