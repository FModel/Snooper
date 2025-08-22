using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture2D(int width, int height,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte)
    : Texture(width, height, TextureTarget.Texture2D, internalFormat, format, type)
{
    public override GetPName Name => GetPName.TextureBinding2D;

    private UTexture? _owner;

    public Texture2D(UTexture texture) : this(texture.PlatformData.SizeX, texture.PlatformData.SizeY)
    {
        _owner = texture;
        
        Guid = _owner.LightingGuid;
    }

    public override void Generate()
    {
        base.Generate();
        if (_owner is null)
        {
            return;
        }
        
        var mip = _owner.GetMipByMaxSize(Settings.MaxTextureMipSize);
        if (mip?.BulkData == null)
            throw new InvalidOperationException("Mip data is null.");

        Resize(_owner.Format, mip, _owner.SRGB);
        Swizzle();
        
        if (_owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap)
        {
            GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
        }
        else
        {
            GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, 8);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        OnTextureReadyForBindless();
        _owner = null;
    }
}
