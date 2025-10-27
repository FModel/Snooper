using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

/// <summary>
/// dedicated framebuffer for object picking
/// uses a single channel 32-bit unsigned integer texture to store component IDs
/// it does not need yet another render pass because the picking is done in both deferred/forward rendering and combined here
///
/// ColorAttachment0 is the outline texture that will be blended over the final image
/// ColorAttachment1 is the picking texture (combined from deferred and forward picking)
/// ColorAttachment2 is the mask texture of the currently selected object
/// ColorAttachment3 is the outline mask texture
/// ColorAttachment4 is a debug texture to see the picking texture
/// </summary>
public class PickingFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    // single channel 32-bit unsigned integer texture for component IDs, then that id can give us the actor, no instance picking yet
    private readonly PickingTexture _picking = new(originalWidth, originalHeight);
    private readonly Texture2D _mask = new(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red);
    private readonly Texture2D _outline = new(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.DepthComponent, false);
    
    private readonly ShaderProgram _combineShader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Picking/combine.frag");
    private readonly ShaderProgram _maskShader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Picking/mask.frag");
    private readonly ShaderProgram _outlineShader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Picking/outline.frag");
    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/picking.frag");

    public override void Generate()
    {
        _picking.Generate();
        _picking.Resize(Width, Height);
        
        _mask.Generate();
        _mask.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
        
        _outline.Generate();
        _outline.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
        
        _depth.Generate();
        _depth.Resize(Width, Height);
        
        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _picking, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, _mask, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, _outline, 0);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
        
        _combineShader.Generate();
        _combineShader.Link();
        
        _maskShader.Generate();
        _maskShader.Link();

        _outlineShader.Generate();
        _outlineShader.Link();
        
        _shader.Generate();
        _shader.Link();
    }

    public void Render()
    {
        // combine deferred and forward picking into one texture
        GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _combineShader.Use();
            _combineShader.SetUniform("deferredPicking", 0);
            _combineShader.SetUniform("forwardPicking", 1);
        });
        
        // use that combined texture to create a mask of the currently selected object
        GL.DrawBuffer(DrawBufferMode.ColorAttachment2);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _maskShader.Use();
            _maskShader.SetUniform("picked", _id);
            _maskShader.SetUniform("pickingTexture", 0);
            _picking.Bind(TextureUnit.Texture0);
        });
        
        // use that mask to create an outline
        GL.DrawBuffer(DrawBufferMode.ColorAttachment3);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _outlineShader.Use();
            _outlineShader.SetUniform("outlineThickness", 2);
            _outlineShader.SetUniform("texelSize", new Vector2(1.0f / Width, 1.0f / Height));
            _outlineShader.SetUniform("selectionMask", 0);
            _mask.Bind(TextureUnit.Texture0);
        });
        
        // use that outline to highlight the selected object with a color
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _shader.Use();
            _shader.SetUniform("outlineColor", new Vector3(1.0f, 0.6f, 0.2f));
            _shader.SetUniform("outlineMask", 0);
            _outline.Bind(TextureUnit.Texture0);
        });
    }
    
    public uint ReadPixel(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        Bind();
        var pixel = 0u;

        var scaleX = windowSize.X / Width;
        var scaleY = windowSize.Y / Height;
        var x = Convert.ToInt32((mousePos.X - windowPos.X) / scaleX);
        var y = -Convert.ToInt32((mousePos.Y - windowPos.Y) / scaleY);

        // picking texture is in color attachment 1 and we the first channel of a single pixel
        GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
        GL.ReadPixels(x, y, 1, 1, PixelFormat.RedInteger, PixelType.UnsignedInt, ref pixel);
        GL.ReadBuffer(ReadBufferMode.None);

        Unbind();
        return pixel;
    }
    
    private uint _id;
    public void OverrideId(uint id)
    {
        _id = id;
    }
    
    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _mask.Resize(newWidth, newHeight);
        _outline.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public override long Allocated
    {
        get
        {
            var total = base.Allocated;
            total += _picking.Allocated;
            total += _mask.Allocated;
            total += _outline.Allocated;
            total += _depth.Allocated;
            total += _combineShader.Allocated;
            total += _maskShader.Allocated;
            total += _outlineShader.Allocated;
            total += _shader.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            var total = base.Used;
            total += _picking.Used;
            total += _mask.Used;
            total += _outline.Used;
            total += _depth.Used;
            total += _combineShader.Used;
            total += _maskShader.Used;
            total += _outlineShader.Used;
            total += _shader.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;
        
        yield return new MemoryDetail("Picking Texture", _picking);
        yield return new MemoryDetail("Mask Texture", _mask);
        yield return new MemoryDetail("Outline Texture", _outline);
        yield return new MemoryDetail("Depth Renderbuffer", _depth);
        yield return new MemoryDetail("Combine Shader", _combineShader);
        yield return new MemoryDetail("Mask Shader", _maskShader);
        yield return new MemoryDetail("Outline Shader", _outlineShader);
        yield return new MemoryDetail("Main Shader", _shader);
    }
}