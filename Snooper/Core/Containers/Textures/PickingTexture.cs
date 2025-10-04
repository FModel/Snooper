using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class PickingTexture(int width, int height) : Texture2D(width, height, PixelInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt)
{
    public override void Generate()
    {
        base.Generate();
        
        Bind();
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }
}

