using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public abstract class FullQuadFramebuffer<TTextureEnum>(
    int originalWidth, int originalHeight,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte) : Framebuffer<TTextureEnum> where TTextureEnum : struct, Enum
{
    public sealed override int Width => Color.Width;
    public sealed override int Height => Color.Height;

    protected readonly ResizableTexture2D Color = new(originalWidth, originalHeight, internalFormat, format, type, "FullQuad - Final Color");

    private readonly VertexArray _vao = new(); // the vertex shader builds the triangle, but a draw call still needs a vao bound

    public override void Generate()
    {
        Color.Generate();
        Color.Resize(Width, Height);
        Color.SetSampling(TextureMinFilter.Linear, TextureMagFilter.Linear);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment0, Color, 0);

        CheckStatus();

        _vao.Generate();
    }

    protected void Render(Action? beginDraw = null)
    {
        _vao.Bind();

        beginDraw?.Invoke();
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        _vao.Unbind();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        Color.Resize(newWidth, newHeight);
    }

    protected override Texture[] CreateTextures() => [Color];

    public override long Allocated
    {
        get
        {
            long total = 0;
            total += Color.Allocated;
            total += _vao.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = 0;
            total += Color.Used;
            total += _vao.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Vertex Array", _vao);
        yield return new MemoryDetail("Color Texture", Color);
    }

    public override void Dispose()
    {
        base.Dispose();

        _vao.Dispose();
        Color.Dispose();
    }
}
