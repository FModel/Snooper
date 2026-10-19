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
    private (TextureMinFilter Min, TextureMagFilter Mag, TextureWrapMode WrapS, TextureWrapMode WrapT) _sampling;
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
        Reset(Width, Height, _levels, !_terrain && FormatInfo is not CompressedTextureFormatInfo); // no mips for a compressed texture cooked without them, the gpu decompresses and re-compresses every level on the cpu, it's laggy
        _levels = null;

        if (!_terrain) Swizzle();
        SetSampling(_sampling.Min, _sampling.Mag, _sampling.WrapS, _sampling.WrapT);

        IsReadyForBindless = true;
    }

    public override void Prepare()
    {
        if (_unsupported || _levels is not null || (_owner is null && _reload is null)) return;

        var owner = _owner ?? _reload?.Invoke();
        if (owner is null)
            throw new InvalidOperationException($"{Name} has no asset to read from.");

        _terrain = owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap;

        (_sampling.Min, _sampling.Mag) = _terrain ? (TextureMinFilter.Linear, TextureMagFilter.Linear)
            : owner.RenderNearestNeighbor ? (TextureMinFilter.NearestMipmapLinear, TextureMagFilter.Nearest)
            : owner.Filter == TextureFilter.TF_Bilinear ? (TextureMinFilter.LinearMipmapNearest, TextureMagFilter.Linear)
            : (TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear);
        (_sampling.WrapS, _sampling.WrapT) = _terrain ? (TextureWrapMode.ClampToEdge, TextureWrapMode.ClampToEdge) : (Wrap(owner.GetTextureAddressX()), Wrap(owner.GetTextureAddressY()));

        TextureWrapMode Wrap(TextureAddress address) => address switch
        {
            TextureAddress.TA_Clamp => TextureWrapMode.ClampToEdge,
            TextureAddress.TA_Mirror => TextureWrapMode.MirroredRepeat,
            _ => TextureWrapMode.Repeat
        };

        var maxSize = Bridge.Options.MaxTextureMipSize;
        if (owner.PlatformData is { FirstMipToSerialize: >= 0, VTData: { } vt } && vt.IsInitialized())
        {
            var mipIndex = owner.GetMipIndexByMaxSize(maxSize);
            if (mipIndex < 0)
                throw new InvalidOperationException("No suitable mip found for the given max texture size.");

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
            // don't use GetMipIndexByMaxSize, it tests the mips through .Data, which keeps their bytes on the asset
            var mips = owner.PlatformData.Mips;
            var levels = new List<byte[]>();
            var first = 0;
            for (; first < mips.Length; first++)
            {
                if (mips[first].SizeX > maxSize && mips[first].SizeY > maxSize && first < mips.Length - 1) continue;
                if (Read(first) is not { } data) continue;

                levels.Add(data);
                break;
            }

            if (levels.Count == 0)
                throw new InvalidOperationException("No suitable mip found for the given max texture size.");

            Width = mips[first].SizeX;
            Height = mips[first].SizeY;
            FormatInfo = owner.Format.GetTextureFormat(owner.SRGB);

            for (var i = first + 1; i < mips.Length && !_terrain; i++)
            {
                if (mips[i].SizeX != Math.Max(1, Width >> levels.Count) || mips[i].SizeY != Math.Max(1, Height >> levels.Count)) break; // stops before GL complains about non-power-of-two mip sizes
                if (Read(i) is not { } data) break;

                levels.Add(data);
            }

            _levels = [.. levels];

            byte[]? Read(int index) => mips[index].BulkData?.ReadDataOnce() is { Length: > 0 } data ? data
                : owner.GetMip(index)?.BulkData?.Data is { Length: > 0 } provided ? provided // a mip data provider (landscape, oodle) sets the bulk data there
                : null;
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
