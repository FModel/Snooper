using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

/// <summary>
/// dedicated framebuffer for object picking
/// uses a single channel 32-bit unsigned integer texture to store component IDs
///
/// it is a full screen quad under the hood so we can easily render it to the screen for debugging
/// ColorAttachment0 is the debug texture (visualization of the picking texture)
/// ColorAttachment1 is the picking texture
///
/// picking is enabled by default for all IPickableSystem
/// </summary>
/// <param name="originalWidth"></param>
/// <param name="originalHeight"></param>
public class PickingFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    // single channel 32-bit unsigned integer texture for component IDs, then that id can give us the actor, no instance picking yet
    private readonly Texture2D _picking = new(originalWidth, originalHeight, PixelInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.DepthComponent, false);
    
    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/picking.frag");

    public override void Generate()
    {
        _picking.Generate();
        _picking.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        
        _depth.Generate();
        _depth.Resize(Width, Height);
        
        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _picking, 0);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
        
        _shader.Generate();
        _shader.Link();
    }
    
    public void Render()
    {
        // this is only used for debug viewing of the picking texture, can be removed later
        base.Render(() =>
        {
            _shader.Use();
            _shader.SetUniform("pickingTexture", 0);
            _picking.Bind(TextureUnit.Texture0);
        });
    }
    
    public uint ReadPixel(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        Bind();
        uint pixel = 0;

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
    
    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }
}