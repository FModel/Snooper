using System.Reflection;
using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Snooper.Core.Containers.Textures;

public class EmbeddedTexture2D(string file,
    int width = 24, int height = 24,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgba8,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte) : Texture2D(width, height, internalFormat, format, type)
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    public override void Generate()
    {
        base.Generate();
        if (FormatInfo is not TextureFormatInfo info) return;
        
        ProcessPixels(info);
        
        GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureWrapR, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
    }
    
    private void ProcessPixels(TextureFormatInfo info)
    {
        var assemblyName = _assembly.GetName().Name;
        using var stream = _assembly.GetManifestResourceStream($"{assemblyName}.UI.Textures.{file.Replace('\\', '.').Replace('/', '.')}");
        if (stream == null)
            throw new FileNotFoundException($"Embedded texture file '{file}' not found in assembly '{assemblyName}'.");
        
        using var img = Image.Load<Rgba32>(stream);
        Resize(img.Width, img.Height);
        
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                GL.TexSubImage2D(Target, 0, 0, y, accessor.Width, 1, info.Format, info.Type, accessor.GetRowSpan(y).ToArray());
            }
        });
    }
}
