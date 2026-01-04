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
            "DIVINEKNOCKOUT" or "MOONMAN" =>
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
    }

    public static ITextureFormatInfo GetTextureFormat(this EPixelFormat format, bool srgb)
    {
        var compressed = format.IsCompressed();
        if (compressed) return new CompressedTextureFormatInfo(format.GetCompressedFormat(srgb));

        var (internalFormat, pixelFormat, pixelType) = format.GetUncompressedFormats(srgb);
        return new TextureFormatInfo(internalFormat, pixelFormat, pixelType);
    }

    public static PixelInternalFormat ToPixelInternalFormat(this SizedInternalFormat format)
    {
        return format switch
        {
            SizedInternalFormat.Rgba8 => PixelInternalFormat.Rgba8,
            SizedInternalFormat.Srgb8Alpha8 => PixelInternalFormat.Srgb8Alpha8,
            SizedInternalFormat.R8 => PixelInternalFormat.R8,
            SizedInternalFormat.Rg8 => PixelInternalFormat.Rg8,
            SizedInternalFormat.Rgba32f => PixelInternalFormat.Rgba32f,
            SizedInternalFormat.Rgb16f => PixelInternalFormat.Rgb16f,
            SizedInternalFormat.Rgba16f => PixelInternalFormat.Rgba16f,
            SizedInternalFormat.R32f => PixelInternalFormat.R32f,
            SizedInternalFormat.Rg16f => PixelInternalFormat.Rg16f,
            SizedInternalFormat.Rg16 => PixelInternalFormat.Rg16,
            SizedInternalFormat.Rg32f => PixelInternalFormat.Rg32f,
            SizedInternalFormat.Rgba16 => PixelInternalFormat.Rgba16,
            SizedInternalFormat.R16f => PixelInternalFormat.R16f,
            SizedInternalFormat.R16 => PixelInternalFormat.R16,
            SizedInternalFormat.Rgb32f => PixelInternalFormat.Rgb32f,
            SizedInternalFormat.R32ui => PixelInternalFormat.R32ui,
            SizedInternalFormat.DepthComponent16 => PixelInternalFormat.DepthComponent16,
            SizedInternalFormat.DepthComponent24 => PixelInternalFormat.DepthComponent24,
            SizedInternalFormat.DepthComponent32f => PixelInternalFormat.DepthComponent32f,

            _ => throw new NotImplementedException($"Unsupported sized internal format: {format}")
        };
    }

    public static InternalFormat ToInternalFormat(this SizedInternalFormat format)
    {
        return format switch
        {
            SizedInternalFormat.Rgba8 => InternalFormat.Rgba8,
            SizedInternalFormat.Srgb8Alpha8 => InternalFormat.Srgb8Alpha8,
            SizedInternalFormat.R8 => InternalFormat.R8,
            SizedInternalFormat.Rgba32f => InternalFormat.Rgba32f,
            SizedInternalFormat.Rgb16f => InternalFormat.Rgb16f,
            SizedInternalFormat.Rgba16f => InternalFormat.Rgba16f,
            SizedInternalFormat.R32f => InternalFormat.R32f,
            SizedInternalFormat.Rg16f => InternalFormat.Rg16f,
            SizedInternalFormat.Rg16 => InternalFormat.Rg16,
            SizedInternalFormat.Rg32f => InternalFormat.Rg32f,
            SizedInternalFormat.Rgba16 => InternalFormat.Rgba16,
            SizedInternalFormat.R16f => InternalFormat.R16f,
            SizedInternalFormat.R16 => InternalFormat.R16,
            SizedInternalFormat.Rgb32f => InternalFormat.Rgb32f,
            SizedInternalFormat.R32ui => InternalFormat.R32ui,

            _ => throw new NotImplementedException($"Unsupported sized internal format: {format}")
        };
    }

    private static bool IsCompressed(this EPixelFormat format)
        => format switch
        {
            EPixelFormat.PF_B8G8R8A8 or
            EPixelFormat.PF_R8G8B8A8 or
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

    private static (SizedInternalFormat, PixelFormat, PixelType) GetUncompressedFormats(this EPixelFormat format, bool srgb)
    {
        return format switch
        {
            EPixelFormat.PF_B8G8R8A8 when srgb => (
                SizedInternalFormat.Srgb8Alpha8,
                PixelFormat.Bgra,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_B8G8R8A8 => (
                SizedInternalFormat.Rgba8,
                PixelFormat.Bgra,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_R8G8B8A8 when srgb => (
                SizedInternalFormat.Srgb8Alpha8,
                PixelFormat.Rgba,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_R8G8B8A8 => (
                SizedInternalFormat.Rgba8,
                PixelFormat.Rgba,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_G8 => (
                SizedInternalFormat.R8,
                PixelFormat.Red,
                PixelType.UnsignedByte
            ),
            EPixelFormat.PF_A32B32G32R32F => (
                SizedInternalFormat.Rgba32f,
                PixelFormat.Rgba,
                PixelType.Float
            ),
            EPixelFormat.PF_FloatRGB => (
                SizedInternalFormat.Rgb16f,
                PixelFormat.Rgb,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_FloatRGBA => (
                SizedInternalFormat.Rgba16f,
                PixelFormat.Rgba,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_R32_FLOAT => (
                SizedInternalFormat.R32f,
                PixelFormat.Red,
                PixelType.Float
            ),
            EPixelFormat.PF_G16R16F or EPixelFormat.PF_G16R16F_FILTER => (
                SizedInternalFormat.Rg16f,
                PixelFormat.Rg,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_G16R16 => (
                SizedInternalFormat.Rg16,
                PixelFormat.Rg,
                PixelType.UnsignedShort
            ),
            EPixelFormat.PF_G32R32F => (
                SizedInternalFormat.Rg32f,
                PixelFormat.Rg,
                PixelType.Float
            ),
            EPixelFormat.PF_A16B16G16R16 => (
                SizedInternalFormat.Rgba16,
                PixelFormat.Rgba,
                PixelType.UnsignedShort
            ),
            EPixelFormat.PF_R16F or EPixelFormat.PF_R16F_FILTER => (
                SizedInternalFormat.R16f,
                PixelFormat.Red,
                PixelType.HalfFloat
            ),
            EPixelFormat.PF_G16 => (
                SizedInternalFormat.R16,
                PixelFormat.Red,
                PixelType.UnsignedShort
            ),
            EPixelFormat.PF_R32G32B32F => (
                SizedInternalFormat.Rgb32f,
                PixelFormat.Rgb,
                PixelType.Float
            ),
            _ => throw new NotImplementedException($"Unsupported pixel format: {format}")
        };
    }

    private static SizedInternalFormat GetCompressedFormat(this EPixelFormat format, bool srgb)
    {
        return format switch
        {
            EPixelFormat.PF_DXT1 when srgb => SizedInternalFormat.CompressedSrgbAlphaS3tcDxt1Ext,
            EPixelFormat.PF_DXT3 when srgb => SizedInternalFormat.CompressedSrgbAlphaS3tcDxt3Ext,
            EPixelFormat.PF_DXT5 when srgb => SizedInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext,
            EPixelFormat.PF_DXT1 => SizedInternalFormat.CompressedRgbaS3tcDxt1Ext,
            EPixelFormat.PF_DXT3 => SizedInternalFormat.CompressedRgbaS3tcDxt3Ext,
            EPixelFormat.PF_DXT5 => SizedInternalFormat.CompressedRgbaS3tcDxt5Ext,
            EPixelFormat.PF_BC4 => SizedInternalFormat.CompressedRedRgtc1,
            EPixelFormat.PF_BC5 => SizedInternalFormat.CompressedRgRgtc2,
            EPixelFormat.PF_BC6H => SizedInternalFormat.CompressedRgbBptcUnsignedFloat,
            EPixelFormat.PF_BC7 => SizedInternalFormat.CompressedRgbaBptcUnorm,

            EPixelFormat.PF_ASTC_4x4 when srgb => SizedInternalFormat.CompressedSrgb8Alpha8Astc4X4,
            EPixelFormat.PF_ASTC_6x6 when srgb => SizedInternalFormat.CompressedSrgb8Alpha8Astc6X6,
            EPixelFormat.PF_ASTC_8x8 when srgb => SizedInternalFormat.CompressedSrgb8Alpha8Astc8X8,
            EPixelFormat.PF_ASTC_10x10 when srgb => SizedInternalFormat.CompressedSrgb8Alpha8Astc10X10,
            EPixelFormat.PF_ASTC_12x12 when srgb => SizedInternalFormat.CompressedSrgb8Alpha8Astc12X12,
            EPixelFormat.PF_ASTC_4x4 => SizedInternalFormat.CompressedRgbaAstc4X4,
            EPixelFormat.PF_ASTC_6x6 => SizedInternalFormat.CompressedRgbaAstc6X6,
            EPixelFormat.PF_ASTC_8x8 => SizedInternalFormat.CompressedRgbaAstc8X8,
            EPixelFormat.PF_ASTC_10x10 => SizedInternalFormat.CompressedRgbaAstc10X10,
            EPixelFormat.PF_ASTC_12x12 => SizedInternalFormat.CompressedRgbaAstc12X12,

            // EPixelFormat.PF_ETC1 when srgb => SizedInternalFormat.CompressedSrgb8Etc2,
            EPixelFormat.PF_ETC2_RGB when srgb => SizedInternalFormat.CompressedSrgb8Etc2,
            EPixelFormat.PF_ETC2_RGBA when srgb => SizedInternalFormat.CompressedSrgb8Alpha8Etc2Eac,
            // EPixelFormat.PF_ETC1 => SizedInternalFormat.CompressedRgb8Etc2,
            EPixelFormat.PF_ETC2_RGB => SizedInternalFormat.CompressedRgb8Etc2,
            EPixelFormat.PF_ETC2_RGBA => SizedInternalFormat.CompressedRgba8Etc2Eac,

            _ => throw new NotImplementedException($"Unsupported pixel format: {format}")
        };
    }
}
