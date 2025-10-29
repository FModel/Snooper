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

    private readonly ResizableTexture2D _position = new(originalWidth, originalHeight, SizedInternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.Float);
    private readonly ResizableTexture2D _normal = new(originalWidth, originalHeight, SizedInternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.Float);
    private readonly ResizableTexture2D _color = new(originalWidth, originalHeight);
    private readonly ResizableTexture2D _specular = new(originalWidth, originalHeight);
    private readonly PickingTexture _picking = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/light.frag");

    public override void Generate()
    {
        _position.Generate();
        _position.Resize(Width, Height);
        GL.TextureParameter(_position, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_position, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_position, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(_position, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        _normal.Generate();
        _normal.Resize(Width, Height);
        GL.TextureParameter(_normal, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_normal, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);

        _color.Generate();
        _color.Resize(Width, Height);
        GL.TextureParameter(_color, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_color, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        
        _specular.Generate();
        _specular.Resize(Width, Height);
        GL.TextureParameter(_specular, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_specular, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        
        _picking.Generate();
        _picking.Resize(Width, Height);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment0, _position, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment1, _normal, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment2, _color, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment3, _specular, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment4, _picking, 0);
        GL.NamedFramebufferDrawBuffers(Handle, 5, [
            DrawBuffersEnum.ColorAttachment0,
            DrawBuffersEnum.ColorAttachment1,
            DrawBuffersEnum.ColorAttachment2,
            DrawBuffersEnum.ColorAttachment3,
            DrawBuffersEnum.ColorAttachment4,
        ]);
        GL.NamedFramebufferRenderbuffer(Handle, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();

        _fullQuad.Generate();

        _shader.Generate();
        _shader.Link();
    }

    public override void Bind(uint unit) => _fullQuad.Bind(unit);
    public void BindPicking(uint unit) => _picking.Bind(unit);

    public void BindTextures(bool position = false, bool normal = false, bool color = false, bool specular = false)
    {
        if (position) _position.Bind(0);
        if (normal) _normal.Bind(1);
        if (color) _color.Bind(2);
        if (specular) _specular.Bind(3);
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
        _picking.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
        _fullQuad.Resize(newWidth, newHeight);
    }

    public override Texture[] GetTextures() =>
    [
        _position,
        _normal,
        _color,
        _specular,
    ];

    public override long Allocated
    {
        get
        {
            long total = 0;
            total += _fullQuad.Allocated;
            total += _position.Allocated;
            total += _normal.Allocated;
            total += _color.Allocated;
            total += _specular.Allocated;
            total += _picking.Allocated;
            total += _depth.Allocated;
            total += _shader.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = 0;
            total += _fullQuad.Used;
            total += _position.Used;
            total += _normal.Used;
            total += _color.Used;
            total += _specular.Used;
            total += _picking.Used;
            total += _depth.Used;
            total += _shader.Used;
            return total;
        }
    }
    
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Full Quad Framebuffer", _fullQuad);
        yield return new MemoryDetail("Position Texture", _position);
        yield return new MemoryDetail("Normal Texture", _normal);
        yield return new MemoryDetail("Color Texture", _color);
        yield return new MemoryDetail("Specular Texture", _specular);
        yield return new MemoryDetail("Picking Texture", _picking);
        yield return new MemoryDetail("Depth Renderbuffer", _depth);
        yield return new MemoryDetail("Main Shader", _shader);
    }
}
