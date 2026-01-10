using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ShadowFramebuffer(int originalWidth, int originalHeight) : Framebuffer
{
    public override int Width => _depth.Width;
    public override int Height => _depth.Height;

    private readonly ResizableTexture2D _depth = new(originalWidth, originalHeight, SizedInternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float);

    public override void Generate()
    {
        _depth.Generate();
        _depth.Resize(Width, Height);
        GL.TextureParameter(_depth, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_depth, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_depth, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToBorder);
        GL.TextureParameter(_depth, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToBorder);
        GL.TextureParameter(_depth, TextureParameterName.TextureBorderColor, [1.0f, 1.0f, 1.0f, 1.0f]);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.DepthAttachment, _depth, 0);
        GL.NamedFramebufferDrawBuffer(Handle, DrawBufferMode.None);
        GL.NamedFramebufferReadBuffer(Handle, ReadBufferMode.None);

        CheckStatus();
    }

    public override void Bind(uint unit) => _depth.Bind(unit);

    public override void Resize(int newWidth, int newHeight)
    {

    }

    public override Texture[] GetTextures() =>
    [
        _depth,
    ];

    public override long Allocated
    {
        get
        {
            long total = 0;
            total += _depth.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = 0;
            total += _depth.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Depth Texture", _depth);
    }

    public override void Dispose()
    {
        base.Dispose();

        _depth.Dispose();
    }
}
