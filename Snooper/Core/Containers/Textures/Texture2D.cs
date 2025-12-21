using CUE4Parse_Conversion.Textures;
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

        var mipIndex = _owner.GetMipIndexByMaxSize(Settings.MaxTextureMipSize);
        if (mipIndex < 0)
            throw new InvalidOperationException("No suitable mip found for the given max texture size.");

        byte[]? mipData;
        int width, height;
        if (_owner.PlatformData is { FirstMipToSerialize: >= 0, VTData: { } vt } && vt.IsInitialized())
        {
            // TODO: decode somewhere else, Generate runs in the render thread
            var textureData = _owner.DecodeMip(mipIndex, ETexturePlatform.DesktopMobile);
            mipData = textureData.Data;
            width = textureData.Width;
            height = textureData.Height;

            FormatInfo = textureData.PixelFormat.GetTextureFormat(_owner.SRGB);
        }
        else
        {
            var mip = _owner.PlatformData.Mips[mipIndex];
            mipData = mip.BulkData?.Data;
            width = mip.SizeX;
            height = mip.SizeY;

            FormatInfo = _owner.Format.GetTextureFormat(_owner.SRGB);
        }

        if (mipData is null)
            throw new InvalidOperationException("Mip data is null.");

        var terrain = _owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap;
        Resize(width, height, mipData, !terrain);
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
