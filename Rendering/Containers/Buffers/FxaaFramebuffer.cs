using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Buffers;

public class FxaaFramebuffer(int originalWidth, int originalHeight) : Framebuffer
{
    public override int Width => _fullQuad.Width;
    public override int Height => _fullQuad.Width;

    private readonly FullQuadFramebuffer _fullQuad = new(originalWidth, originalHeight);

    private readonly Texture2D _color = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(Width, Height);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _color.Target, _color, 0);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();

        _fullQuad.Generate();
    }

    public override void Bind(TextureUnit unit) => _fullQuad.Bind(unit);

    public override void Render(Action<ShaderProgram>? callback = null)
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Handle);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fullQuad);
        GL.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        // TODO: Implement fxaa rendering logic here
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
        _fullQuad.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _fullQuad.GetPointer();
}
