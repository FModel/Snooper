using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;

namespace Snooper.UI.Containers.Textures;

public class FramebufferTexture(int width, int height) : Texture2D(width, height)
{
    public override void Generate()
    {
        base.Generate();
        Bind(TextureUnit.Texture0);

        GL.TexImage2D(Target, 0, PixelInternalFormat.Rgb, Width, Height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero);

        GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, Target, Handle, 0);
    }
}
