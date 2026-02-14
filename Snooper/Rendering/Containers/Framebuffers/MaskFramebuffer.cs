using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class MaskFramebuffer(int originalWidth, int originalHeight) : Framebuffer<EMaskTexture>
{
    public override int Width => _depth.Width;
    public override int Height => _depth.Height;

    private readonly ResizableTexture2D _depth = new(originalWidth, originalHeight, SizedInternalFormat.DepthComponent16, PixelFormat.DepthComponent, PixelType.Float, "Mask - Depth");

    public override void Generate()
    {
        _depth.Generate();
        _depth.Resize(Width, Height);
        GL.TextureParameter(_depth, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_depth, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_depth, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(_depth, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.DepthAttachment, _depth, 0);
        GL.NamedFramebufferDrawBuffer(Handle, DrawBufferMode.None);
        GL.NamedFramebufferReadBuffer(Handle, ReadBufferMode.None);

        CheckStatus();
    }

    public override void Bind(EMaskTexture texture, uint unit)
    {
        if (texture != EMaskTexture.Depth)
            throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid mask texture type");

        _depth.Bind(unit);
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _depth.Resize(newWidth, newHeight);
    }

    public override Texture[] GetTextures() => [];

    public override long Allocated => _depth.Allocated;
    public override long Used => _depth.Used;
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
