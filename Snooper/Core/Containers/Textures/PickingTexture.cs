using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class PickingTexture(int width, int height, string? name = null) : ResizableTexture2D(width, height, SizedInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt, name)
{
    public override void Generate()
    {
        base.Generate();
        SetSampling(TextureMinFilter.Nearest, TextureMagFilter.Nearest, TextureWrapMode.ClampToEdge);
    }
}

