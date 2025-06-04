using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class Texture2D(
    int width,
    int height,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgb,
    PixelFormat format = PixelFormat.Rgb,
    PixelType type = PixelType.UnsignedByte)
    : Texture(
        width,
        height,
        TextureTarget.Texture2D,
        internalFormat,
        format,
        type);
