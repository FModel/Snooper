using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Buffers;

public class GeometryBuffer(int originalWidth, int originalHeight) : Framebuffer
{
    public override int Width => _fullQuad.Width;
    public override int Height => _fullQuad.Width;

    private readonly FullQuadFramebuffer _fullQuad = new(originalWidth, originalHeight);

    private readonly Texture2D _position = new(originalWidth, originalHeight, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.Float);
    private readonly Texture2D _normal = new(originalWidth, originalHeight, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.Float);
    private readonly Texture2D _color = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    private readonly ShaderProgram _shader = new(
"""
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec2 vTexCoords;

void main()
{
    gl_Position = vec4(aPos, 0.0, 1.0);
    vTexCoords = aTexCoords;
}
""",
"""
#version 330 core

in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gColor;
uniform sampler2D ssao;

out vec4 FragColor;

void main()
{
    vec3 normal = texture(gNormal, vTexCoords).rgb;
    vec4 color = texture(gColor, vTexCoords);
    float ao = texture(ssao, vTexCoords).r;

    float brightness = 0.7 + 0.3 * normal.z;

    FragColor = vec4(color.rgb * brightness * ao, color.a);
}
""");

    public override void Generate()
    {
        _position.Generate();
        _position.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        _normal.Generate();
        _normal.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);

        _color.Generate();
        _color.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _position, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _normal, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, _color, 0);
        GL.DrawBuffers(3, [
            DrawBuffersEnum.ColorAttachment0,
            DrawBuffersEnum.ColorAttachment1,
            DrawBuffersEnum.ColorAttachment2,
        ]);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();

        _fullQuad.Generate();

        _shader.Generate();
        _shader.Link();
    }

    public override void Bind()
    {
        base.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Disable(EnableCap.Blend);
    }

    public override void Bind(TextureUnit unit) => _fullQuad.Bind(unit);

    public void BindSsao()
    {
        _position.Bind(TextureUnit.Texture0);
        _normal.Bind(TextureUnit.Texture1);
    }

    public override void Render()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Handle);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fullQuad);
        GL.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        BindSsao();
        _color.Bind(TextureUnit.Texture2);

        _shader.Use();
        _shader.SetUniform("gPosition", 0);
        _shader.SetUniform("gNormal", 1);
        _shader.SetUniform("gColor", 2);
        _shader.SetUniform("ssao", 3);

        _fullQuad.Render();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _position.Resize(newWidth, newHeight);
        _normal.Resize(newWidth, newHeight);
        _color.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
        _fullQuad.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _fullQuad.GetPointer();
}
