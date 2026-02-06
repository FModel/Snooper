using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class OutlineFramebuffer(int originalWidth, int originalHeight) : Framebuffer
{
    public override int Width => _color.Width;
    public override int Height => _color.Height;

    private readonly ResizableTexture2D _color = new(originalWidth, originalHeight);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(Width, Height);
        GL.TextureParameter(_color, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_color, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment0, _color, 0);

        CheckStatus();
    }

    public override void Bind(uint texture, uint unit) => Bind(unit);
    public override void Bind(uint unit) => _color.Bind(unit);

    public override void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
    }

    public override Texture[] GetTextures() => [_color];

    public override long Allocated => _color.Allocated;
    public override long Used => _color.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Color Texture", _color);
    }

    public override void Dispose()
    {
        base.Dispose();

        _color.Dispose();
    }
}
