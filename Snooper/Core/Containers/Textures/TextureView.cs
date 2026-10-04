using OpenTK.Graphics.OpenGL4;
using Snooper.Extensions;

namespace Snooper.Core.Containers.Textures;

/// <summary>
/// a second GL name over the storage of a texture, with its own parameters
/// </summary>
public class TextureView(Texture source, bool linear = false, int channel = -1) : HandledObject
{
    public bool Red = true;
    public bool Green = true;
    public bool Blue = true;
    public bool Alpha;

    public override void Generate()
    {
        if (Handle > 0)
            throw new InvalidOperationException("Texture view already generated.");

        uint original = source;
        GL.GetTextureLevelParameter(original, 0, GetTextureParameter.TextureInternalFormat, out int format);
        if (linear) format = (int) ((SizedInternalFormat) format).ToLinear();

        var handle = GL.GenTexture();
        GL.TextureView(handle, source.Target, (int) original, (PixelInternalFormat) format, 0, source.MipCount, 0, 1);
        Handle = (uint) handle;

        GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int) source.MinFilter);
        GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int) source.MagFilter);
        UpdateChannels();
    }

    public void UpdateChannels()
    {
        int[] sources = [(int) PixelFormat.Red, (int) PixelFormat.Green, (int) PixelFormat.Blue, (int) PixelFormat.Alpha];
        var alpha = Alpha ? sources[3] : (int) All.One;

        int[] mask = (Red, Green, Blue) switch
        {
            _ when channel is >= 0 and <= 3 => [sources[channel], sources[channel], sources[channel], (int) All.One],
            (true, false, false) => [sources[0], sources[0], sources[0], alpha],
            (false, true, false) => [sources[1], sources[1], sources[1], alpha],
            (false, false, true) => [sources[2], sources[2], sources[2], alpha],
            _ => [Red ? sources[0] : (int) All.Zero, Green ? sources[1] : (int) All.Zero, Blue ? sources[2] : (int) All.Zero, alpha]
        };
        GL.TextureParameter(Handle, TextureParameterName.TextureSwizzleRgba, mask);
    }

    public IntPtr GetPointer() => (IntPtr) Handle;

    public override void Dispose()
    {
        if (Handle == 0) return;

        GL.DeleteTexture(Handle);
        Handle = 0;
    }

    public override long Allocated => 0;
    public override long Used => 0;
}
