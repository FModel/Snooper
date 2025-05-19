using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.UI.Containers.Textures;

namespace Snooper.UI.Containers;

public class MsaaFramebuffer(int width, int height) : Framebuffer
{
    private readonly FramebufferTexture _texture = new(width, height);
    private readonly Renderbuffer _renderbuffer = new();

    private readonly Framebuffer _ppFramebuffer = new();
    private readonly MsaaTexture _ppTexture = new(width, height);

    private readonly VertexArray _vertexArray = new();
    private readonly ArrayBuffer<float> _vertexBuffer = new([
        1.0f, -1.0f,  1.0f, 0.0f,
        -1.0f, -1.0f,  0.0f, 0.0f,
        -1.0f,  1.0f,  0.0f, 1.0f,

        1.0f,  1.0f,  1.0f, 1.0f,
        1.0f, -1.0f,  1.0f, 0.0f,
        -1.0f,  1.0f,  0.0f, 1.0f
    ]);
    private readonly ElementArrayBuffer<ushort> _indexBuffer = new([0, 1, 2, 3, 4, 5]);

    private readonly ShaderProgram _shader = new(
@"#version 460 core

layout (location = 0) in vec2 vPos;
layout (location = 1) in vec2 vTexCoords;

out vec2 fTexCoords;

void main()
{
    gl_Position = vec4(vPos.x, vPos.y, 0.0, 1.0);
    fTexCoords = vTexCoords;
}",
@"#version 460 core

in vec2 fTexCoords;

uniform sampler2D screenTexture;

out vec4 FragColor;

void main()
{
    FragColor = texture(screenTexture, fTexCoords);
}");

    public override void Generate()
    {
        base.Generate();
        base.Bind();

        _texture.Generate();

        _renderbuffer.Generate();
        _renderbuffer.Bind();
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Settings.NumberOfSamples, RenderbufferStorage.Depth24Stencil8, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _renderbuffer);

        _vertexBuffer.Generate();
        _vertexBuffer.Bind();

        _indexBuffer.Generate();
        _indexBuffer.Bind();

        _vertexArray.Generate();
        _vertexArray.Bind();

        var stride = _vertexBuffer.Stride;
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);

        _shader.Generate();
        _shader.Link();

        _shader.Use();
        _shader.SetUniform("screenTexture", 0);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Framebuffer failed to bind with error: {GL.GetProgramInfoLog(Handle)}");
        }

        _ppFramebuffer.Generate();
        _ppFramebuffer.Bind();
        _ppTexture.Generate();

        status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Post-Processing framebuffer failed to bind with error: {GL.GetProgramInfoLog(_ppFramebuffer)}");
        }
    }

    public override void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Handle);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _ppFramebuffer);
        GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        _shader.Use();
        _vertexBuffer.Bind();
        _indexBuffer.Bind();
        _vertexArray.Bind();

        _ppTexture.Bind(TextureUnit.Texture0);

        GL.Disable(EnableCap.DepthTest);
        GL.DrawArrays(PrimitiveType.Triangles, 0, _indexBuffer.Size);
        GL.Enable(EnableCap.DepthTest);
    }

    public IntPtr GetPointer() => _ppTexture.GetPointer();
}
