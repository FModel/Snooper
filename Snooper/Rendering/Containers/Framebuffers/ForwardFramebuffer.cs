using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ForwardFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly PickingTexture _picking = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    public override void Generate()
    {
        _picking.Generate();
        _picking.Resize(Width, Height);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment1, _picking, 0);
        GL.NamedFramebufferDrawBuffers(Handle, 2, [DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1]);
        GL.NamedFramebufferRenderbuffer(Handle, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
    }

    public void BindPicking(uint unit) => _picking.Bind(unit);

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public override long Allocated => base.Allocated + _picking.Allocated + _depth.Allocated;
    public override long Used => base.Used + _picking.Used + _depth.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Picking Texture", _picking);
        yield return new MemoryDetail("Depth Renderbuffer", _depth);
    }

    public override void Dispose()
    {
        base.Dispose();

        _picking.Dispose();
        _depth.Dispose();
    }
}
