using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public interface ITextureFormatInfo
{
    public SizedInternalFormat InternalFormat { get; }

    public long GetMemorySize(int width, int height, int depth = 1);
}

public readonly struct TextureFormatInfo : ITextureFormatInfo
{
    public SizedInternalFormat InternalFormat { get; }
    public readonly PixelFormat Format;
    public readonly PixelType Type;

    private readonly int _bytesPerPixel;

    public TextureFormatInfo(SizedInternalFormat internalFormat, PixelFormat format, PixelType type)
    {
        InternalFormat = internalFormat;
        Format = format;
        Type = type;

        _bytesPerPixel = GetBytesPerPixel();
    }

    public long GetMemorySize(int width, int height, int depth = 1)
    {
        return (long)width * height * depth * _bytesPerPixel;
    }

    private int GetBytesPerPixel()
    {
        return InternalFormat switch
        {
            // 8-bit formats
            SizedInternalFormat.R8 => 1,
            SizedInternalFormat.Rg8 => 2,
            SizedInternalFormat.Rgb8 or SizedInternalFormat.Srgb8 => 3,
            SizedInternalFormat.Rgba8 or SizedInternalFormat.Srgb8Alpha8 => 4,

            // 16-bit formats
            SizedInternalFormat.R16 => 2,
            SizedInternalFormat.R16f => 2,
            SizedInternalFormat.Rg16 => 4,
            SizedInternalFormat.Rg16f => 4,
            SizedInternalFormat.Rgb16 or SizedInternalFormat.Rgb16f => 6,
            SizedInternalFormat.Rgba16 or SizedInternalFormat.Rgba16f => 8,

            // 32-bit formats
            SizedInternalFormat.R32f => 4,
            SizedInternalFormat.Rg32f => 8,
            SizedInternalFormat.Rgb32f => 12,
            SizedInternalFormat.Rgba32f => 16,

            // Depth and stencil formats
            SizedInternalFormat.DepthComponent16 => 2,
            SizedInternalFormat.DepthComponent24 => 3,
            SizedInternalFormat.DepthComponent32f => 4,
            SizedInternalFormat.Depth24Stencil8 => 4,
            SizedInternalFormat.Depth32fStencil8 => 5,

            _ => Type switch
            {
                PixelType.UnsignedByte => 4, // Default RGBA8
                PixelType.Float => 16, // Default RGBA32F
                PixelType.HalfFloat => 8, // Default RGBA16F
                PixelType.UnsignedShort => 8, // Default RGBA16
                _ => 4
            }
        };
    }
}

public readonly struct CompressedTextureFormatInfo : ITextureFormatInfo
{
    public SizedInternalFormat InternalFormat { get; }

    private readonly int _blockSize;
    private readonly int _blockWidth;
    private readonly int _blockHeight;

    public CompressedTextureFormatInfo(SizedInternalFormat internalFormat)
    {
        InternalFormat = internalFormat;

        _blockSize = GetBlockSize();
        (_blockWidth, _blockHeight) = GetBlockDimensions();
    }

    public long GetMemorySize(int width, int height, int depth = 1)
    {
        var blocksWide = (width + _blockWidth - 1) / _blockWidth;
        var blocksHigh = (height + _blockHeight - 1) / _blockHeight;

        return (long)blocksWide * blocksHigh * depth * _blockSize;
    }

    private int GetBlockSize()
    {
        return InternalFormat switch
        {
            SizedInternalFormat.CompressedRgbaS3tcDxt1Ext or
            SizedInternalFormat.CompressedSrgbAlphaS3tcDxt1Ext or
            SizedInternalFormat.CompressedRedRgtc1 => 8,

            SizedInternalFormat.CompressedRgbaS3tcDxt3Ext or
            SizedInternalFormat.CompressedSrgbAlphaS3tcDxt3Ext or
            SizedInternalFormat.CompressedRgbaS3tcDxt5Ext or
            SizedInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext or
            SizedInternalFormat.CompressedRgRgtc2 or
            SizedInternalFormat.CompressedRgbBptcUnsignedFloat or
            SizedInternalFormat.CompressedRgbaBptcUnorm => 16,

            _ when IsAstcFormat() => 16,

            _ when IsEtc2RgbFormat() => 8,
            _ when IsEtc2RgbaFormat() => 16,

            _ => 16
        };
    }

    private (int width, int height) GetBlockDimensions()
    {
        return InternalFormat switch
        {
            SizedInternalFormat.CompressedRgbaAstc4X4 or SizedInternalFormat.CompressedSrgb8Alpha8Astc4X4 => (4, 4),
            SizedInternalFormat.CompressedRgbaAstc6X6 or SizedInternalFormat.CompressedSrgb8Alpha8Astc6X6 => (6, 6),
            SizedInternalFormat.CompressedRgbaAstc8X8 or SizedInternalFormat.CompressedSrgb8Alpha8Astc8X8 => (8, 8),
            SizedInternalFormat.CompressedRgbaAstc10X10 or SizedInternalFormat.CompressedSrgb8Alpha8Astc10X10 => (10, 10),
            SizedInternalFormat.CompressedRgbaAstc12X12 or SizedInternalFormat.CompressedSrgb8Alpha8Astc12X12 => (12, 12),

            _ => (4, 4)
        };
    }

    private bool IsAstcFormat()
    {
        return InternalFormat is
            SizedInternalFormat.CompressedRgbaAstc4X4 or SizedInternalFormat.CompressedSrgb8Alpha8Astc4X4 or
            SizedInternalFormat.CompressedRgbaAstc6X6 or SizedInternalFormat.CompressedSrgb8Alpha8Astc6X6 or
            SizedInternalFormat.CompressedRgbaAstc8X8 or SizedInternalFormat.CompressedSrgb8Alpha8Astc8X8 or
            SizedInternalFormat.CompressedRgbaAstc10X10 or SizedInternalFormat.CompressedSrgb8Alpha8Astc10X10 or
            SizedInternalFormat.CompressedRgbaAstc12X12 or SizedInternalFormat.CompressedSrgb8Alpha8Astc12X12;
    }

    private bool IsEtc2RgbFormat()
    {
        return InternalFormat is SizedInternalFormat.CompressedRgb8Etc2 or SizedInternalFormat.CompressedSrgb8Etc2;
    }

    private bool IsEtc2RgbaFormat()
    {
        return InternalFormat is SizedInternalFormat.CompressedRgba8Etc2Eac or SizedInternalFormat.CompressedSrgb8Alpha8Etc2Eac;
    }
}
