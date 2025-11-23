using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class FullQuadFramebuffer(
    int originalWidth, int originalHeight,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte) : Framebuffer
{
    public override int Width => _color.Width;
    public override int Height => _color.Height;

    private readonly ResizableTexture2D _color = new(originalWidth, originalHeight, internalFormat, format, type);
    private readonly VertexArray _vao = new();
    private readonly ArrayBuffer<Vector4> _vbo = new();
    private readonly ElementArrayBuffer<uint> _ebo = new();

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(originalWidth, originalHeight);
        GL.TextureParameter(_color, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_color, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment0, _color, 0);

        CheckStatus();

        _vao.Generate();
        _vbo.Generate();
        _ebo.Generate();

        _vbo.AddRange(
        [
            new Vector4(1.0f, -1.0f, 1.0f, 0.0f),
            new Vector4(-1.0f, -1.0f, 0.0f, 0.0f),
            new Vector4(-1.0f, 1.0f, 0.0f, 1.0f),
            new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        ]);
        _ebo.AddRange([0, 1, 2, 3, 0, 2]);

        GL.VertexArrayVertexBuffer(_vao, 0, _vbo, 0, _vbo.Stride);
        GL.VertexArrayElementBuffer(_vao, _ebo);
        GL.VertexArrayAttribFormat(_vao, 0, 2, VertexAttribType.Float, false, 0);
        GL.VertexArrayAttribFormat(_vao, 1, 2, VertexAttribType.Float, false, 8);
        GL.EnableVertexArrayAttrib(_vao, 0);
        GL.EnableVertexArrayAttrib(_vao, 1);
        GL.VertexArrayAttribBinding(_vao, 0, 0);
        GL.VertexArrayAttribBinding(_vao, 1, 0);
    }

    public override void Bind(uint unit) => _color.Bind(unit);

    public void Render(Action? beginDraw = null)
    {
        _vao.Bind();
        _ebo.Bind();
        _vbo.Bind();

        beginDraw?.Invoke();
        GL.DrawElements(PrimitiveType.Triangles, _ebo.Count, DrawElementsType.UnsignedInt, 0);

        _vbo.Unbind();
        _ebo.Unbind();
        _vao.Unbind();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
    }

    public override Texture[] GetTextures() => [_color];

    public override long Allocated
    {
        get
        {
            long total = 0;
            total += _color.Allocated;
            total += _vao.Allocated;
            total += _vbo.Allocated;
            total += _ebo.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = 0;
            total += _color.Used;
            total += _vao.Used;
            total += _vbo.Used;
            total += _ebo.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Vertex Array", _vao);
        yield return new MemoryDetail("Vertex Buffer", _vbo);
        yield return new MemoryDetail("Index Buffer", _ebo);
        yield return new MemoryDetail("Color Texture", _color);
    }

    public override void Dispose()
    {
        base.Dispose();

        _vao.Dispose();
        _vbo.Dispose();
        _ebo.Dispose();
        _color.Dispose();
    }
}
