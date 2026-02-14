using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Snooper.Core.Containers.Textures;

public class EmbeddedTexture2D(string file,
    int width = 24, int height = 24, bool mipmapped = false,
    SizedInternalFormat internalFormat = SizedInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte) : Texture2D(width, height, internalFormat, format, type, Path.GetFileName(file))
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    public override void Generate()
    {
        base.Generate();
        if (FormatInfo is not TextureFormatInfo info) return;

        ProcessPixels(info);

        GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.TextureParameter(Handle, TextureParameterName.TextureWrapR, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(Handle, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(Handle, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
    }

    private void ProcessPixels(TextureFormatInfo info)
    {
        var assemblyName = _assembly.GetName().Name;
        using var stream = _assembly.GetManifestResourceStream($"{assemblyName}.{file.Replace('\\', '.').Replace('/', '.')}");
        if (stream == null)
            throw new FileNotFoundException($"Embedded texture file '{file}' not found in assembly '{assemblyName}'.");

        using var img = Image.Load<Rgba32>(stream);
        Reset<nint>(img.Width, img.Height, [], mipmapped); // this is simply gonna allocate the storage

        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                GL.TextureSubImage2D(Handle, 0, 0, y, accessor.Width, 1, info.Format, info.Type, accessor.GetRowSpan(y).ToArray());
            }
        });
    }
}
