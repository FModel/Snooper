using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Extensions;

namespace Snooper.Core.Containers.Textures;

public class Texture2D(int width, int height,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte,
    string? name = null)
    : Texture(width, height, TextureTarget.Texture2D, internalFormat, format, type, name)
{
    private UTexture? _owner;

    public Texture2D(UTexture texture) : this(texture.PlatformData.SizeX, texture.PlatformData.SizeY, name: texture.Name)
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

        FormatInfo = _owner.Format.GetTextureFormat(_owner.SRGB);

        var terrain = _owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap;
        Resize(mip.SizeX, mip.SizeY, mip.BulkData.Data, !terrain);
        Log.Debug("Texture {Guid} of format {Format} uploaded to GPU with size {Width}x{Height}.", Guid, _owner.Format, Width, Height);
        
        if (terrain)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TextureParameter(Handle, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TextureParameter(Handle, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
        }
        else
        {
            Swizzle();
            GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
            GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.GenerateTextureMipmap(Handle);
        }
        
        OnTextureReadyForBindless();
        _owner = null;
    }
}
