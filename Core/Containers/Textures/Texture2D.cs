using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture2D() : Texture(TextureTarget.Texture2D)
{
    public override GetPName PName { get => GetPName.Texture2D; }
}
