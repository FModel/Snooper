using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Buffers;

public class PostProcFramebuffer(int originalWidth, int originalHeight, ShaderProgram shader) : Framebuffer
{
    private readonly Texture2D _color = new(originalWidth, originalHeight);

    private readonly VertexArray _vao = new();
    private readonly ArrayBuffer<Vector4> _vbo = new(4, BufferUsageHint.StaticDraw);
    private readonly ElementArrayBuffer<uint> _ebo = new(6, BufferUsageHint.StaticDraw);

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
        _vbo.SetData([
            new Vector4(1.0f, -1.0f, 1.0f, 0.0f),
            new Vector4(-1.0f, -1.0f, 0.0f, 0.0f),
            new Vector4(-1.0f, 1.0f, 0.0f, 1.0f),
            new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        ]);

        _ebo.Generate();
        _ebo.Bind();
        _ebo.SetData([0, 1, 2, 3, 0, 2]);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, _vbo.Stride, 0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, _vbo.Stride, 8);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);

        shader.Generate();
        shader.Link();
    }

    public void Render(Action<ShaderProgram> uniforms)
    {
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Handle);
        GL.BlitFramebuffer(0, 0, _color.Width, _color.Height, 0, 0, _color.Width, _color.Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        // shader.Use();
        // uniforms(shader);
        //
        // _vao.Bind();
        // GL.DrawElements(PrimitiveType.Triangles, _ebo.Size, DrawElementsType.UnsignedInt, 0);
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _color.GetPointer();
}
