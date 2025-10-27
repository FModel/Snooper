using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public interface ITextureFormatInfo
{
    long GetMemorySize(int width, int height, int depth = 1);
}

public readonly struct TextureFormatInfo : ITextureFormatInfo
{
    public readonly PixelInternalFormat InternalFormat;
    public readonly PixelFormat Format;
    public readonly PixelType Type;
    
    private readonly int _bytesPerPixel;

    public TextureFormatInfo(PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
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
            PixelInternalFormat.R8 => 1,
            PixelInternalFormat.Rg8 => 2,
            PixelInternalFormat.Rgb8 or PixelInternalFormat.Srgb8 => 3,
            PixelInternalFormat.Rgba8 or PixelInternalFormat.Srgb8Alpha8 => 4,
            
            // 16-bit formats
            PixelInternalFormat.R16 => 2,
            PixelInternalFormat.R16f => 2,
            PixelInternalFormat.Rg16 => 4,
            PixelInternalFormat.Rg16f => 4,
            PixelInternalFormat.Rgb16 or PixelInternalFormat.Rgb16f => 6,
            PixelInternalFormat.Rgba16 or PixelInternalFormat.Rgba16f => 8,
            
            // 32-bit formats
            PixelInternalFormat.R32f => 4,
            PixelInternalFormat.Rg32f => 8,
            PixelInternalFormat.Rgb32f => 12,
            PixelInternalFormat.Rgba32f => 16,
            
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
    public readonly InternalFormat InternalFormat;
    
    private readonly int _blockSize;
    private readonly int _blockWidth;
    private readonly int _blockHeight;

    public CompressedTextureFormatInfo(InternalFormat internalFormat)
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
            InternalFormat.CompressedRgbaS3tcDxt1Ext or
            InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext or
            InternalFormat.CompressedRedRgtc1 => 8,
            
            InternalFormat.CompressedRgbaS3tcDxt3Ext or
            InternalFormat.CompressedSrgbAlphaS3tcDxt3Ext or
            InternalFormat.CompressedRgbaS3tcDxt5Ext or
            InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext or
            InternalFormat.CompressedRgRgtc2 or
            InternalFormat.CompressedRgbBptcUnsignedFloat or
            InternalFormat.CompressedRgbaBptcUnorm => 16,
            
            _ when IsAstcFormat() => 16,
            
            _ when IsEtc2RgbFormat() => 8,
            _ when IsEtc2RgbaFormat() => 16,
            
            _ => 16
        };
    }
    
    private (int width, int height) GetBlockDimensions()
    {
        var f = (int)InternalFormat;
        return InternalFormat switch
        {
            _ when f is (int)All.CompressedRgbaAstc4X4 or (int)All.CompressedSrgb8Alpha8Astc4X4 => (4, 4),
            _ when f is (int)All.CompressedRgbaAstc6X6 or (int)All.CompressedSrgb8Alpha8Astc6X6 => (6, 6),
            _ when f is (int)All.CompressedRgbaAstc8X8 or (int)All.CompressedSrgb8Alpha8Astc8X8 => (8, 8),
            _ when f is (int)All.CompressedRgbaAstc10X10 or (int)All.CompressedSrgb8Alpha8Astc10X10 => (10, 10),
            _ when f is (int)All.CompressedRgbaAstc12X12 or (int)All.CompressedSrgb8Alpha8Astc12X12 => (12, 12),
            
            _ => (4, 4)
        };
    }
    
    private bool IsAstcFormat()
    {
        return (int)InternalFormat is 
            (int)All.CompressedRgbaAstc4X4 or (int)All.CompressedSrgb8Alpha8Astc4X4 or
            (int)All.CompressedRgbaAstc6X6 or (int)All.CompressedSrgb8Alpha8Astc6X6 or
            (int)All.CompressedRgbaAstc8X8 or (int)All.CompressedSrgb8Alpha8Astc8X8 or
            (int)All.CompressedRgbaAstc10X10 or (int)All.CompressedSrgb8Alpha8Astc10X10 or
            (int)All.CompressedRgbaAstc12X12 or (int)All.CompressedSrgb8Alpha8Astc12X12;
    }
    
    private bool IsEtc2RgbFormat()
    {
        return (int)InternalFormat is (int)All.CompressedRgb8Etc2 or (int)All.CompressedSrgb8Etc2;
    }
    
    private bool IsEtc2RgbaFormat()
    {
        return (int)InternalFormat is (int)All.CompressedRgba8Etc2Eac or (int)All.CompressedSrgb8Alpha8Etc2Eac;
    }
}