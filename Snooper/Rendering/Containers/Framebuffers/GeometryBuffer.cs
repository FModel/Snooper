using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class GeometryBuffer(int originalWidth, int originalHeight) : Framebuffer
{
    public override int Width => _fullQuad.Width;
    public override int Height => _fullQuad.Height;

    private readonly FullQuadFramebuffer _fullQuad = new(originalWidth, originalHeight);

    private readonly Texture2D _position = new(originalWidth, originalHeight, PixelInternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.Float);
    private readonly Texture2D _normal = new(originalWidth, originalHeight, PixelInternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.Float);
    private readonly Texture2D _color = new(originalWidth, originalHeight);
    private readonly Texture2D _specular = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/light.frag");

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
        
        _specular.Generate();
        _specular.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _position, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _normal, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, _color, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, _specular, 0);
        GL.DrawBuffers(4, [
            DrawBuffersEnum.ColorAttachment0,
            DrawBuffersEnum.ColorAttachment1,
            DrawBuffersEnum.ColorAttachment2,
            DrawBuffersEnum.ColorAttachment3,
        ]);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();

        _fullQuad.Generate();

        _shader.Generate();
        _shader.Link();
    }

    public override void Bind(TextureUnit unit) => _fullQuad.Bind(unit);

    public void BindTextures(bool position = false, bool normal = false, bool color = false, bool specular = false)
    {
        if (position) _position.Bind(TextureUnit.Texture0);
        if (normal) _normal.Bind(TextureUnit.Texture1);
        if (color) _color.Bind(TextureUnit.Texture2);
        if (specular) _specular.Bind(TextureUnit.Texture3);
    }

    public void Render(Action<ShaderProgram> callback)
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Handle);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fullQuad);
        GL.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        _fullQuad.Render(() =>
        {
            BindTextures(true, true, true, true);

            _shader.Use();
            _shader.SetUniform("gPosition", 0);
            _shader.SetUniform("gNormal", 1);
            _shader.SetUniform("gColor", 2);
            _shader.SetUniform("gSpecular", 3);
            callback.Invoke(_shader);
        });
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _position.Resize(newWidth, newHeight);
        _normal.Resize(newWidth, newHeight);
        _color.Resize(newWidth, newHeight);
        _specular.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
        _fullQuad.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _fullQuad.GetPointer();
    public IntPtr[] GetTexturePointers() =>
    [
        _position.GetPointer(),
        _normal.GetPointer(),
        _color.GetPointer(),
        _specular.GetPointer(),
    ];
}
