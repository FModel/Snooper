using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers;

public class Framebuffer(int originalWidth, int originalHeight) : Core.Containers.Framebuffer, IResizable
{
    private readonly Texture2D _color = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(originalWidth, originalHeight);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        
        _depth.Generate();
        _depth.Resize(originalWidth, originalHeight);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _color, 0);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Framebuffer failed to bind with error: {GL.GetProgramInfoLog(Handle)}");
        }
    }

    public void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public IntPtr GetPointer() => _color.GetPointer();
}
