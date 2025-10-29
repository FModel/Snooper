using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class PickingTexture(int width, int height, string? name = null) : ResizableTexture2D(width, height, SizedInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt, name)
{
    public override void Generate()
    {
        base.Generate();
        
        GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TextureParameter(Handle, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(Handle, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }
}

