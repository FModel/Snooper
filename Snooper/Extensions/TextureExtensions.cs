using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;

namespace Snooper.Extensions;

public static class TextureExtensions
{
    public static void SwizzlePerGame(this Texture texture, string game)
    {
        texture.SwizzleMask = game switch
        {
            // R: Whatever (AO / S / E / ...)
            // G: Roughness
            // B: Metallic
            "GAMEFACE" or "HK_PROJECT" or "COSMICSHAKE" or "PHOENIX" or "ATOMICHEART" or "MULTIVERSUS" or "BODYCAM" =>
            [
                (int)PixelFormat.Red, (int)PixelFormat.Blue, (int)PixelFormat.Green, (int)PixelFormat.Alpha
            ],
            // R: Metallic
            // G: Roughness
            // B: Whatever (AO / S / E / ...)
            "SHOOTERGAME" or "DIVINEKNOCKOUT" or "MOONMAN" =>
            [
                (int)PixelFormat.Blue, (int)PixelFormat.Red, (int)PixelFormat.Green, (int)PixelFormat.Alpha
            ],
            // R: Roughness
            // G: Metallic
            // B: Whatever (AO / S / E / ...)
            "CCFF7R" or "PJ033" =>
            [
                (int)PixelFormat.Blue, (int)PixelFormat.Green, (int)PixelFormat.Red, (int)PixelFormat.Alpha
            ],
            _ => texture.SwizzleMask
        };
        texture.Swizzle();
    }
    
    public static ITextureFormatInfo GetTextureFormat(this EPixelFormat format)
    {
        var compressed = format.IsCompressed();
        if (compressed) return new CompressedTextureFormatInfo(format.GetCompressedFormat());
        
        var (internalFormat, pixelFormat, pixelType) = format.GetUncompressedFormats();
        return new TextureFormatInfo(internalFormat, pixelFormat, pixelType);
    }
    
    private static bool IsCompressed(this EPixelFormat format)
        => format switch
        {
            EPixelFormat.PF_B8G8R8A8 or
                EPixelFormat.PF_G8 or
                EPixelFormat.PF_A32B32G32R32F or
                EPixelFormat.PF_FloatRGB or
                EPixelFormat.PF_FloatRGBA or
                EPixelFormat.PF_R32_FLOAT or
                EPixelFormat.PF_G16R16F or
                EPixelFormat.PF_G16R16F_FILTER or
                EPixelFormat.PF_G16R16 or
                EPixelFormat.PF_G32R32F or
                EPixelFormat.PF_A16B16G16R16 or
                EPixelFormat.PF_R16F or
                EPixelFormat.PF_R16F_FILTER or
                EPixelFormat.PF_G16 or
                EPixelFormat.PF_R32G32B32F => false,
            _ => true
        };
    
    private static (PixelInternalFormat, PixelFormat, PixelType) GetUncompressedFormats(this EPixelFormat format)
    {
        return format switch
        {
            EPixelFormat.PF_B8G8R8A8 => (
                PixelInternalFormat.Rgba8,
                PixelFormat.Bgra,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_G8 => (
                PixelInternalFormat.R8,
                PixelFormat.Red,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_A32B32G32R32F => (
                PixelInternalFormat.Rgba32f,
                PixelFormat.Rgba,
                PixelType.Float
            ),
            EPixelFormat.PF_FloatRGB => (
                PixelInternalFormat.Rgb16f,
                PixelFormat.Rgb,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_FloatRGBA => (
                PixelInternalFormat.Rgba16f,
                PixelFormat.Rgba,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_R32_FLOAT => (
                PixelInternalFormat.R32f,
                PixelFormat.Red,
                PixelType.Float
            ),
            EPixelFormat.PF_G16R16F or EPixelFormat.PF_G16R16F_FILTER => (
                PixelInternalFormat.Rg16f,
                PixelFormat.Rg,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_G16R16 => (
                PixelInternalFormat.Rg16,
                PixelFormat.Rg,
                PixelType.UnsignedShort
            ),
            EPixelFormat.PF_G32R32F => (
                PixelInternalFormat.Rg32f,
                PixelFormat.Rg,
                PixelType.Float
            ),
            EPixelFormat.PF_A16B16G16R16 => (
                PixelInternalFormat.Rgba16,
                PixelFormat.Rgba,
                PixelType.UnsignedShort
            ),
            EPixelFormat.PF_R16F or EPixelFormat.PF_R16F_FILTER => (
                PixelInternalFormat.R16f,
                PixelFormat.Red,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_G16 => (
                PixelInternalFormat.R16,
                PixelFormat.Red,
                PixelType.UnsignedShort
            ),
            EPixelFormat.PF_R32G32B32F => (
                PixelInternalFormat.Rgb32f,
                PixelFormat.Rgb,
                PixelType.Float
            ),
            _ => throw new NotImplementedException($"Unsupported pixel format: {format}")
        };
    }
    
    private static InternalFormat GetCompressedFormat(this EPixelFormat format)
    {
        return format switch
        {
            EPixelFormat.PF_DXT1 => InternalFormat.CompressedRgbaS3tcDxt1Ext,
            EPixelFormat.PF_DXT3 => InternalFormat.CompressedRgbaS3tcDxt3Ext,
            EPixelFormat.PF_DXT5 => InternalFormat.CompressedRgbaS3tcDxt5Ext,
            EPixelFormat.PF_BC4 => InternalFormat.CompressedRedRgtc1,
            EPixelFormat.PF_BC5 => InternalFormat.CompressedRgRgtc2,
            EPixelFormat.PF_BC6H => InternalFormat.CompressedRgbBptcUnsignedFloat,
            EPixelFormat.PF_BC7 => InternalFormat.CompressedRgbaBptcUnorm,
        
            EPixelFormat.PF_ASTC_4x4 => (InternalFormat)All.CompressedRgbaAstc4X4,
            EPixelFormat.PF_ASTC_6x6 => (InternalFormat)All.CompressedRgbaAstc6X6,
            EPixelFormat.PF_ASTC_8x8 => (InternalFormat)All.CompressedRgbaAstc8X8,
            EPixelFormat.PF_ASTC_10x10 => (InternalFormat)All.CompressedRgbaAstc10X10,
            EPixelFormat.PF_ASTC_12x12 => (InternalFormat)All.CompressedRgbaAstc12X12,
            
            // EPixelFormat.PF_ETC1 => (InternalFormat)All.CompressedRgb8Etc2,
            EPixelFormat.PF_ETC2_RGB => (InternalFormat)All.CompressedRgb8Etc2,
            EPixelFormat.PF_ETC2_RGBA => (InternalFormat)All.CompressedRgba8Etc2Eac,
        
            _ => throw new NotImplementedException($"Unsupported pixel format: {format}")
        };
    }
}