using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ForwardFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly Texture2D _picking = new(originalWidth, originalHeight, PixelInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, false);

    public override void Generate()
    {
        _picking.Generate();
        _picking.Resize(Width, Height);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
        
        _depth.Generate();
        _depth.Resize(Width, Height);
        
        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _picking, 0);
        GL.DrawBuffers(2, [
            DrawBuffersEnum.ColorAttachment0,
            DrawBuffersEnum.ColorAttachment1,
        ]);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();
    }
    
    public void BindPicking(TextureUnit unit) => _picking.Bind(unit);

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }
}
