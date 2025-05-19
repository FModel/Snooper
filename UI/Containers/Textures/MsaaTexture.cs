using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;

namespace Snooper.UI.Containers.Textures;

public class MsaaTexture(int width, int height) : Texture(width, height, TextureTarget.Texture2DMultisample)
{
    public override void Generate()
    {
        base.Generate();
        Bind(TextureUnit.Texture0);

        GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, Settings.NumberOfSamples, PixelInternalFormat.Rgb, Width, Height, true);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, Target, Handle, 0);
    }
}
