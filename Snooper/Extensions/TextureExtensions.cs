using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures.BC;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Hardware;
using Snooper.Rendering.Cache;

namespace Snooper.Extensions;

public static class TextureExtensions
{
    // the default swizzle layout per game, then the layouts of specific texture suffixes in that game, which win over it
    private static readonly Dictionary<string, (string Default, (string Suffix, string Layout)[] Suffixes)> _maskLayouts = new()
    {
        ["GAMEFACE"] = ("RM?",[]),
        ["HK_PROJECT"] = ("?RM",[("LP", "MR?")]),
        ["COSMICSHAKE"] = ("?RM",[]),
        ["PHOENIX"] = ("?RM",[]),
        ["ATOMICHEART"] = ("?RM",[]),
        ["MULTIVERSUS"] = ("?RM",[]),
        ["BODYCAM"] = ("?RM",[]),
        ["SANDFALL"] = ("?RM",[]),
        ["MARVEL"] = ("?RM",[]),
        ["DIVINEKNOCKOUT"] = ("MR?",[]),
        ["MOONMAN"] = ("MR?",[]),
        ["CCFF7R"] = ("RM?",[]),
        ["PJ033"] = ("RM?",[]),
    };

    private static readonly (string Word, string Letter)[] _channelWords =
    [
        ("AmbientOcclusion", "O"),
        ("Occlusion", "O"),
        ("Roughness", "R"),
        ("Metallic", "M"),
        ("Metalness", "M"),
        ("Specular", "S"),
        ("Emissive", "E"),
        ("Height", "H"),
        ("AO", "O")
    ];

    /// <summary>
    /// output must be R: Specular, G: Metallic, B: Roughness
    /// </summary>
    public static void SwizzlePerName(this Texture texture, string name, string game, out EMaskChannels missing)
    {
        var suffix = name.AsSpan().TrimEnd("0123456789").TrimEnd('_'); // exclude _ORM2, ORM_01
        suffix = suffix[(suffix.LastIndexOf('_') + 1)..];

        if (_maskLayouts.TryGetValue(game, out var layouts))
        {
            foreach (var (ending, layout) in layouts.Suffixes)
            {
                if (!suffix.Equals(ending, StringComparison.OrdinalIgnoreCase)) continue;

                Swizzle(layout, out missing);
                return;
            }
        }

        var letters = suffix.ToString();
        foreach (var (word, letter) in _channelWords)
        {
            letters = letters.Replace(word, letter, StringComparison.OrdinalIgnoreCase);
        }

        // glued to a word (BaseORM), the layout is the capitals the name ends with
        var capitals = letters.AsSpan(letters.AsSpan().LastIndexOfAnyExceptInRange('A', 'Z') + 1);
        if (Swizzle(letters, out missing) || Swizzle(capitals, out missing)) return;

        if (layouts.Default is not { } fallback || !Swizzle(fallback, out missing))
        {
            missing = EMaskChannels.None; // trust the raw channels
        }

        bool Swizzle(ReadOnlySpan<char> layout, out EMaskChannels absent)
        {
            const string channels = "AOMRSEH?";

            absent = EMaskChannels.None;
            if (layout.Length is not (3 or 4)) return false;

            Span<int> sources = [(int) PixelFormat.Red, (int) PixelFormat.Green, (int) PixelFormat.Blue, (int) PixelFormat.Alpha];
            int specular = -1, metallic = -1, roughness = -1;
            for (var i = 0; i < layout.Length; i++)
            {
                var letter = char.ToUpperInvariant(layout[i]);
                if (!channels.Contains(letter)) return false;

                switch (letter)
                {
                    case 'S' when specular < 0: specular = i; break;
                    case 'M' when metallic < 0: metallic = i; break;
                    case 'R' when roughness < 0: roughness = i; break;
                    case 'S' or 'M' or 'R': return false; // the same letter twice is a word, not a layout
                }
            }

            if (specular < 0 && metallic < 0 && roughness < 0) return false;
            if (specular < 0) absent |= EMaskChannels.Specular;
            if (metallic < 0) absent |= EMaskChannels.Metallic;
            if (roughness < 0) absent |= EMaskChannels.Roughness;

            texture.SwizzleMask = [Source(sources, specular), Source(sources, metallic), Source(sources, roughness), (int) PixelFormat.Alpha];
            return true;

            static int Source(Span<int> sources, int channel) => channel < 0 ? (int) All.Zero : sources[channel];
        }
    }

    public static ITextureFormatInfo? GetTextureFormat(this EPixelFormat format, bool srgb)
    {
        return format switch
        {
            EPixelFormat.PF_R8G8B8A8 => Pixels(SizedInternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, SizedInternalFormat.Srgb8Alpha8),
            EPixelFormat.PF_B8G8R8A8 or EPixelFormat.PF_A8R8G8B8 => Pixels(SizedInternalFormat.Rgba8, PixelFormat.Bgra, PixelType.UnsignedByte, SizedInternalFormat.Srgb8Alpha8),
            EPixelFormat.PF_G8 => Pixels(SizedInternalFormat.R8, PixelFormat.Red, PixelType.UnsignedByte),
            EPixelFormat.PF_V8U8 => Pixels(SizedInternalFormat.Rg8Snorm, PixelFormat.Rg, PixelType.Byte),
            EPixelFormat.PF_A32B32G32R32F => Pixels(SizedInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float),
            EPixelFormat.PF_FloatRGB => Pixels(SizedInternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.HalfFloat),
            EPixelFormat.PF_FloatRGBA => Pixels(SizedInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat),
            EPixelFormat.PF_R32_FLOAT => Pixels(SizedInternalFormat.R32f, PixelFormat.Red, PixelType.Float),
            EPixelFormat.PF_G16R16F or EPixelFormat.PF_G16R16F_FILTER => Pixels(SizedInternalFormat.Rg16f, PixelFormat.Rg, PixelType.HalfFloat),
            EPixelFormat.PF_G16R16 => Pixels(SizedInternalFormat.Rg16, PixelFormat.Rg, PixelType.UnsignedShort),
            EPixelFormat.PF_G32R32F => Pixels(SizedInternalFormat.Rg32f, PixelFormat.Rg, PixelType.Float),
            EPixelFormat.PF_A16B16G16R16 => Pixels(SizedInternalFormat.Rgba16, PixelFormat.Rgba, PixelType.UnsignedShort),
            EPixelFormat.PF_R16F or EPixelFormat.PF_R16F_FILTER => Pixels(SizedInternalFormat.R16f, PixelFormat.Red, PixelType.HalfFloat),
            EPixelFormat.PF_G16 => Pixels(SizedInternalFormat.R16, PixelFormat.Red, PixelType.UnsignedShort),
            EPixelFormat.PF_R32G32B32F => Pixels(SizedInternalFormat.Rgb32f, PixelFormat.Rgb, PixelType.Float),

            EPixelFormat.PF_DXT1 => Blocks(SizedInternalFormat.CompressedRgbaS3tcDxt1Ext, SizedInternalFormat.CompressedSrgbAlphaS3tcDxt1Ext),
            EPixelFormat.PF_DXT3 => Blocks(SizedInternalFormat.CompressedRgbaS3tcDxt3Ext, SizedInternalFormat.CompressedSrgbAlphaS3tcDxt3Ext),
            EPixelFormat.PF_DXT5 => Blocks(SizedInternalFormat.CompressedRgbaS3tcDxt5Ext, SizedInternalFormat.CompressedSrgbAlphaS3tcDxt5Ext),
            EPixelFormat.PF_BC4 => Blocks(SizedInternalFormat.CompressedRedRgtc1),
            EPixelFormat.PF_BC5 => Blocks(SizedInternalFormat.CompressedRgRgtc2),
            EPixelFormat.PF_BC6H => Blocks(SizedInternalFormat.CompressedRgbBptcUnsignedFloat),
            EPixelFormat.PF_BC6H_Signed => Blocks(SizedInternalFormat.CompressedRgbBptcSignedFloat),
            EPixelFormat.PF_BC7 => Blocks(SizedInternalFormat.CompressedRgbaBptcUnorm, SizedInternalFormat.CompressedSrgbAlphaBptcUnorm),

            EPixelFormat.PF_ASTC_4x4 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc4X4, SizedInternalFormat.CompressedSrgb8Alpha8Astc4X4),
            EPixelFormat.PF_ASTC_6x6 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc6X6, SizedInternalFormat.CompressedSrgb8Alpha8Astc6X6),
            EPixelFormat.PF_ASTC_8x5 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc8X5, SizedInternalFormat.CompressedSrgb8Alpha8Astc8X5),
            EPixelFormat.PF_ASTC_8x6 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc8X6, SizedInternalFormat.CompressedSrgb8Alpha8Astc8X6),
            EPixelFormat.PF_ASTC_8x8 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc8X8, SizedInternalFormat.CompressedSrgb8Alpha8Astc8X8),
            EPixelFormat.PF_ASTC_10x8 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc10X8, SizedInternalFormat.CompressedSrgb8Alpha8Astc10X8),
            EPixelFormat.PF_ASTC_10x10 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc10X10, SizedInternalFormat.CompressedSrgb8Alpha8Astc10X10),
            EPixelFormat.PF_ASTC_12x12 when DeviceInfo.HasAstc => Blocks(SizedInternalFormat.CompressedRgbaAstc12X12, SizedInternalFormat.CompressedSrgb8Alpha8Astc12X12),

            EPixelFormat.PF_ETC1 => Blocks(SizedInternalFormat.CompressedRgb8Etc2, SizedInternalFormat.CompressedSrgb8Etc2),
            EPixelFormat.PF_ETC2_RGB => Blocks(SizedInternalFormat.CompressedRgb8Etc2, SizedInternalFormat.CompressedSrgb8Etc2),
            EPixelFormat.PF_ETC2_RGBA => Blocks(SizedInternalFormat.CompressedRgba8Etc2Eac, SizedInternalFormat.CompressedSrgb8Alpha8Etc2Eac),
            EPixelFormat.PF_ETC2_R11 or EPixelFormat.PF_ETC2_R11_EAC => Blocks(SizedInternalFormat.CompressedR11Eac),
            EPixelFormat.PF_ETC2_RG11_EAC => Blocks(SizedInternalFormat.CompressedRg11Eac),

            _ => null
        };

        ITextureFormatInfo Pixels(SizedInternalFormat linear, PixelFormat pixelFormat, PixelType type, SizedInternalFormat? srgbFormat = null) => new TextureFormatInfo(srgb ? srgbFormat ?? linear : linear, pixelFormat, type, linear, PixelFormatUtils.PixelFormats[format].Name);
        ITextureFormatInfo Blocks(SizedInternalFormat linear, SizedInternalFormat? srgbFormat = null) => new CompressedTextureFormatInfo(srgb ? srgbFormat ?? linear : linear, linear, PixelFormatUtils.PixelFormats[format]);
    }

    public static PixelInternalFormat ToPixelInternalFormat(this SizedInternalFormat format)
    {
        return format switch
        {
            SizedInternalFormat.Rgba8 => PixelInternalFormat.Rgba8,
            SizedInternalFormat.Srgb8Alpha8 => PixelInternalFormat.Srgb8Alpha8,
            SizedInternalFormat.R8 => PixelInternalFormat.R8,
            SizedInternalFormat.Rg8 => PixelInternalFormat.Rg8,
            SizedInternalFormat.Rgb8 => PixelInternalFormat.Rgb8,
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

    public static bool TryGetFaceAverages(this UTextureCube texture, Span<Vector3> faces)
    {
        if (texture.PlatformData.Mips is not { Length: > 0 } mips || mips[^1].BulkData?.Data is not { } data) return false;

        faces.Clear();
        switch (texture.Format)
        {
            case EPixelFormat.PF_FloatRGBA:
            {
                var texels = MemoryMarshal.Cast<byte, Half>(data);
                var perFace = texels.Length / 4 / faces.Length;
                if (perFace == 0) return false;

                for (var face = 0; face < faces.Length; face++)
                {
                    for (var i = face * perFace * 4; i < (face + 1) * perFace * 4; i += 4)
                    {
                        faces[face] += new Vector3((float) texels[i], (float) texels[i + 1], (float) texels[i + 2]) / perFace;
                    }
                }
                return true;
            }
            case EPixelFormat.PF_DXT1:
            {
                // a block is two 565 colours then two bits a texel, which pick one of them or a blend: its first texel is enough
                var perFace = data.Length / 8 / faces.Length;
                if (perFace == 0) return false;

                for (var face = 0; face < faces.Length; face++)
                {
                    for (var i = face * perFace * 8; i < (face + 1) * perFace * 8; i += 8)
                    {
                        var first = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i));
                        var second = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i + 2));
                        var (a, b) = (Rgb565(first), Rgb565(second));
                        var texel = (data[i + 4] & 3) switch
                        {
                            0 => a,
                            1 => b,
                            2 => first > second ? (a * 2 + b) / 3 : (a + b) / 2,
                            _ => first > second ? (a + b * 2) / 3 : Vector3.Zero
                        };
                        faces[face] += (texture.SRGB ? ToLinear(texel) : texel) / perFace;
                    }
                }
                return true;
            }
            case EPixelFormat.PF_BC6H:
            {
                // a block is two 64-bit words, decoded to 16 texels of four halves: its first texel is enough
                var words = MemoryMarshal.Cast<byte, ulong>(data);
                var perFace = words.Length / 2 / faces.Length;
                if (perFace == 0) return false;

                Span<ulong> block = stackalloc ulong[16];
                var texel = MemoryMarshal.Cast<ulong, Half>(block);
                for (var face = 0; face < faces.Length; face++)
                {
                    for (var i = face * perFace * 2; i < (face + 1) * perFace * 2; i += 2)
                    {
                        BCDecoder.DecodeBC6HBlock(words[i], words[i + 1], block);
                        faces[face] += new Vector3((float) texel[0], (float) texel[1], (float) texel[2]) / perFace;
                    }
                }
                return true;
            }
            default:
#if DEBUG
                throw new NotSupportedException($"Cubemap {texture.Name} is {texture.Format}, which is not read.");
#else
                return false;
#endif
        }

        static Vector3 Rgb565(ushort color) => new((color >> 11) / 31.0f, ((color >> 5) & 63) / 63.0f, (color & 31) / 31.0f);
        static Vector3 ToLinear(Vector3 color) => new(Channel(color.X), Channel(color.Y), Channel(color.Z));
        static float Channel(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}
