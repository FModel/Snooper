using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Extensions;
using Snooper.Hosting;

namespace Snooper.Core.Containers.Textures;

public class Texture2D(int width, int height,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte,
    FGuid? guid = null,
    string? name = null)
    : Texture(width, height, TextureTarget.Texture2D, internalFormat, format, type, guid, name)
{
    private UTexture? _owner;
    private readonly Func<UTexture?>? _reload;
    private readonly bool _unsupported;
    private bool _terrain;
    private byte[][]? _levels;

    public Texture2D(UTexture texture) : this(texture.PlatformData.SizeX, texture.PlatformData.SizeY, guid: texture.LightingGuid, name: texture.Name)
    {
        _owner = texture;
        _unsupported = texture is UTexture2DArray or UTextureCube or UVolumeTexture or UTextureCubeArray or UTextureRenderTarget;

        if (texture.Owner?.Provider is { } provider)
        {
            var path = texture.GetPathName();
            _reload = () =>
            {
                try
                {
                    return provider.LoadPackageObject<UTexture>(path);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Could not reload {Path} for a re-upload", path);
                    return null;
                }
            };
        }
    }

    public override void Generate()
    {
        base.Generate();
        if (_unsupported) return;

        Prepare(); // does nothing when a worker already did, or when there is no asset behind this texture
        if (_levels is null) return;
        Reset(Width, Height, _levels, !_terrain);
        _levels = null;

        if (_terrain)
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
        }

        IsReadyForBindless = true;
    }

    public override void Prepare()
    {
        if (_unsupported || _levels is not null || (_owner is null && _reload is null)) return;

        var owner = _owner ?? _reload?.Invoke();
        if (owner is null)
            throw new InvalidOperationException($"{Name} has no asset to read from.");

        var mipIndex = owner.GetMipIndexByMaxSize(Bridge.Options.MaxTextureMipSize);
        if (mipIndex < 0)
            throw new InvalidOperationException("No suitable mip found for the given max texture size.");

        _terrain = owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap;

        if (owner.PlatformData is { FirstMipToSerialize: >= 0, VTData: { } vt } && vt.IsInitialized())
        {
            var textureData = owner.DecodeMip(mipIndex, Bridge.Options.TexturePlatform);
            if (textureData is null)
                throw new InvalidOperationException("Virtual texture could not be decoded.");

            Width = textureData.Width;
            Height = textureData.Height;
            FormatInfo = textureData.PixelFormat.GetTextureFormat(owner.SRGB);
            _levels = [textureData.Data];
        }
        else
        {
            var mips = owner.PlatformData.Mips;
            var first = mips[mipIndex];

            Width = first.SizeX;
            Height = first.SizeY;
            FormatInfo = owner.Format.GetTextureFormat(owner.SRGB);

            var levels = new List<byte[]>();
            var expectedWidth = Width;
            var expectedHeight = Height;
            for (var i = mipIndex; i < mips.Length; i++)
            {
                var mip = mips[i];
                if (mip.SizeX != expectedWidth || mip.SizeY != expectedHeight) break;

                var data = mip.BulkData?.Data;
                if (data is not { Length: > 0 }) break;

                levels.Add(data);
                if (_terrain) break;

                expectedWidth = Math.Max(1, expectedWidth >> 1);
                expectedHeight = Math.Max(1, expectedHeight >> 1);
            }

            if (levels.Count == 0)
                throw new InvalidOperationException("Mip data is null.");

            _levels = [.. levels];
        }

        _owner = null;
    }

    protected sealed override void SetStorage()
    {
        GL.TextureStorage2D(Handle, MipCount, FormatInfo.InternalFormat, Width, Height);
    }

    protected sealed override void SetPixels<T8>(int mip, int width, int height, T8[] pixels)
    {
        switch (FormatInfo)
        {
            case TextureFormatInfo info:
                GL.TextureSubImage2D(Handle, mip, 0, 0, width, height, info.Format, info.Type, pixels);
                break;
            case CompressedTextureFormatInfo compressed:
                GL.CompressedTextureSubImage2D(Handle, mip, 0, 0, width, height, (PixelFormat)compressed.InternalFormat, pixels.Length, pixels);
                break;
            default:
                throw new NotSupportedException("Unknown texture format info.");
        }
    }
}
