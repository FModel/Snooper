using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class FullQuadFramebuffer(
    int originalWidth, int originalHeight,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte) : Framebuffer
{
    public override int Width => _color.Width;
    public override int Height => _color.Height;

    private readonly Texture2D _color = new(originalWidth, originalHeight, internalFormat, format, type);
    private readonly VertexArray _vao = new();
    private readonly ArrayBuffer<Vector4> _vbo = new(4);
    private readonly ElementArrayBuffer<uint> _ebo = new(6);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(originalWidth, originalHeight);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _color, 0);

        CheckStatus();

        _vao.Generate();
        _vao.Bind();

        _vbo.Generate();
        _vbo.Bind();
        _vbo.Allocate([
            new Vector4(1.0f, -1.0f, 1.0f, 0.0f),
            new Vector4(-1.0f, -1.0f, 0.0f, 0.0f),
            new Vector4(-1.0f, 1.0f, 0.0f, 1.0f),
            new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        ]);

        _ebo.Generate();
        _ebo.Bind();
        _ebo.Allocate([0, 1, 2, 3, 0, 2]);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, _vbo.Stride, 0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, _vbo.Stride, 8);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);

        _vao.Unbind();
        _vbo.Unbind();
        _ebo.Unbind();
    }

    public override void Bind(TextureUnit unit) => _color.Bind(unit);

    public void Render(Action? beginDraw = null)
    {
        _vao.Bind();
        _ebo.Bind();

        beginDraw?.Invoke();
        GL.DrawElements(PrimitiveType.Triangles, _ebo.Count, DrawElementsType.UnsignedInt, 0);

        // _vao.Unbind();
        // _ebo.Unbind();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _color.GetPointer();
}
