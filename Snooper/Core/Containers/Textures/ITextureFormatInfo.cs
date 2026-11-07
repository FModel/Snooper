using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public interface ITextureFormatInfo
{
    public SizedInternalFormat InternalFormat { get; }
    public SizedInternalFormat LinearFormat { get; }

    public long GetMemorySize(int width, int height, int depth = 1);

    public bool IsSrgb => InternalFormat != LinearFormat;
}

public readonly struct TextureFormatInfo : ITextureFormatInfo
{
    public SizedInternalFormat InternalFormat { get; }
    public SizedInternalFormat LinearFormat { get; }
    public readonly PixelFormat Format;
    public readonly PixelType Type;

    private readonly int _bytesPerPixel;
    private readonly string? _name;

    public TextureFormatInfo(SizedInternalFormat internalFormat, PixelFormat format, PixelType type, SizedInternalFormat? linearFormat = null, string? name = null)
    {
        InternalFormat = internalFormat;
        LinearFormat = linearFormat ?? internalFormat;
        Format = format;
        Type = type;
        _name = name;

        _bytesPerPixel = GetBytesPerPixel();
    }

    public long GetMemorySize(int width, int height, int depth = 1)
    {
        return (long)width * height * depth * _bytesPerPixel;
    }

    private int GetBytesPerPixel()
    {
        return LinearFormat switch
        {
            SizedInternalFormat.R8 => 1,
            SizedInternalFormat.Rg8 or SizedInternalFormat.Rg8Snorm => 2,
            SizedInternalFormat.Rgb8 or SizedInternalFormat.Srgb8 => 3,
            SizedInternalFormat.Rgba8 or SizedInternalFormat.Srgb8Alpha8 => 4,

            SizedInternalFormat.R16 or SizedInternalFormat.R16f => 2,
            SizedInternalFormat.Rg16 or SizedInternalFormat.Rg16f => 4,
            SizedInternalFormat.Rgb16 or SizedInternalFormat.Rgb16f => 6,
            SizedInternalFormat.Rgba16 or SizedInternalFormat.Rgba16f => 8,

            SizedInternalFormat.R32f or SizedInternalFormat.R32ui => 4,
            SizedInternalFormat.Rg32f => 8,
            SizedInternalFormat.Rgb32f => 12,
            SizedInternalFormat.Rgba32f => 16,

            SizedInternalFormat.DepthComponent16 => 2,
            SizedInternalFormat.DepthComponent24 => 3,
            SizedInternalFormat.DepthComponent32f or SizedInternalFormat.Depth24Stencil8 => 4,
            SizedInternalFormat.Depth32fStencil8 => 5,

            _ => 0
        };
    }

    public override string ToString() => _name ?? InternalFormat.ToString();
}

public readonly struct CompressedTextureFormatInfo(SizedInternalFormat internalFormat, SizedInternalFormat linearFormat, FPixelFormatInfo blocks) : ITextureFormatInfo
{
    public SizedInternalFormat InternalFormat { get; } = internalFormat;
    public SizedInternalFormat LinearFormat { get; } = linearFormat;

    public long GetMemorySize(int width, int height, int depth = 1)
    {
        var blocksWide = (width + blocks.BlockSizeX - 1) / blocks.BlockSizeX;
        var blocksHigh = (height + blocks.BlockSizeY - 1) / blocks.BlockSizeY;

        return (long)blocksWide * blocksHigh * depth * blocks.BlockBytes;
    }

    public override string ToString() => blocks.Name;
}
