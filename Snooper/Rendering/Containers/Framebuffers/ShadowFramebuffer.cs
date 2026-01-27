using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ShadowFramebuffer(int size, int cascadeCount) : Framebuffer
{
    public override int Width => _depth.Width;
    public override int Height => _depth.Height;
    public int CascadeCount => _depth.Depth;

    private readonly Texture3D _depth = new(size, size, cascadeCount, SizedInternalFormat.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float);

    public override void Generate()
    {
        _depth.Generate();
        _depth.Reset<int>(Width, Height, []);
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
        // shadow map size is fixed
    }

    public override Texture[] GetTextures() => [];

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
