using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class Renderbuffer(int width, int height, RenderbufferStorage storage, bool multisampled) : HandledObject, IResizable
{
    private int _width = width;
    private int _height = height;
    
    private readonly int _bytesPerPixel = storage switch
    {
        RenderbufferStorage.R8 => 1,
        RenderbufferStorage.Rg8 => 2,
        RenderbufferStorage.Rgb8 or RenderbufferStorage.Srgb8 => 3,
        RenderbufferStorage.Rgba8 or RenderbufferStorage.Srgb8Alpha8 => 4,
        RenderbufferStorage.R16 => 2,
        RenderbufferStorage.R16f => 2,
        RenderbufferStorage.Rg16 => 4,
        RenderbufferStorage.Rg16f => 4,
        RenderbufferStorage.Rgb16 or RenderbufferStorage.Rgb16f => 6,
        RenderbufferStorage.Rgba16 or RenderbufferStorage.Rgba16f => 8,
        RenderbufferStorage.R32f => 4,
        RenderbufferStorage.Rg32f => 8,
        RenderbufferStorage.Rgb32f => 12,
        RenderbufferStorage.Rgba32f => 16,
        _ => 4
    };

    public override void Generate()
    {
        GL.CreateRenderbuffers(1, out uint handle);
        Handle = handle;
    }

    public void Resize(int newWidth, int newHeight)
    {
        _width = newWidth;
        _height = newHeight;

        if (multisampled)
        {
            GL.NamedRenderbufferStorageMultisample(Handle, Settings.NumberOfSamples, storage, _width, _height);
        }
        else
        {
            GL.NamedRenderbufferStorage(Handle, storage, _width, _height);
        }
    }

    public override void Dispose()
    {
        GL.DeleteRenderbuffer(Handle);
    }

    public override long Allocated => (long)_width * _height * _bytesPerPixel;
    public override long Used => Allocated;
}
