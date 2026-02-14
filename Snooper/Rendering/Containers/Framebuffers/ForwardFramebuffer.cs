using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ForwardFramebuffer(int originalWidth, int originalHeight) : Framebuffer<EForwardTexture>
{
    public override int Width => _color.Width;
    public override int Height => _color.Height;

    private readonly ResizableTexture2D _color = new(originalWidth, originalHeight, name: "Forward - Color");
    private readonly PickingTexture _picking = new(originalWidth, originalHeight, name: "Forward - Picking");
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(Width, Height);
        GL.TextureParameter(_color, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_color, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _picking.Generate();
        _picking.Resize(Width, Height);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment0, _color, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment1, _picking, 0);
        GL.NamedFramebufferDrawBuffers(Handle, 2, [DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1]);
        GL.NamedFramebufferRenderbuffer(Handle, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
    }

    public override void Bind(EForwardTexture texture, uint unit)
    {
        var t = texture switch
        {
            EForwardTexture.Color => _color,
            EForwardTexture.Picking => _picking,
            _ => throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid forward texture type")
        };

        t.Bind(unit);
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public override Texture[] GetTextures() => [_color];

    public override long Allocated => _color.Allocated + _picking.Allocated + _depth.Allocated;
    public override long Used => _color.Used + _picking.Used + _depth.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Color Texture", _color);
        yield return new MemoryDetail("Picking Texture", _picking);
        yield return new MemoryDetail("Depth Renderbuffer", _depth);
    }

    public override void Dispose()
    {
        base.Dispose();

        _color.Dispose();
        _picking.Dispose();
        _depth.Dispose();
    }
}
