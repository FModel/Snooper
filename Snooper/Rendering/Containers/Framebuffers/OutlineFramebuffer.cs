using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class OutlineFramebuffer(int originalWidth, int originalHeight) : Framebuffer<EOutlineTexture>
{
    public override int Width => _color.Width;
    public override int Height => _color.Height;

    private readonly ResizableTexture2D _color = new(originalWidth, originalHeight, name: "Outline - Color");

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

    public override void Bind(EOutlineTexture texture, uint unit)
    {
        if (texture != EOutlineTexture.Color)
            throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid outline texture type");

        _color.Bind(unit);
    }

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
