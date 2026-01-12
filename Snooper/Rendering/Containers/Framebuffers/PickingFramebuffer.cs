using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

/// <summary>
/// dedicated framebuffer for object picking
/// uses a single channel 32-bit unsigned integer texture to store component IDs
/// it does not need yet another render pass because the picking is done in both deferred/forward rendering and combined here
///
/// ColorAttachment0 is the outline texture that will be blended over the final image
/// ColorAttachment1 is the picking texture (combined from deferred and forward picking)
/// ColorAttachment2 is the mask texture of the currently selected object
/// ColorAttachment3 is the outline mask texture
/// ColorAttachment4 is a debug texture to see the picking texture
/// </summary>
public class PickingFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    // single channel 32-bit unsigned integer texture for component IDs, then that id can give us the actor, no instance picking yet
    private readonly PickingTexture _picking = new(originalWidth, originalHeight);
    private readonly ResizableTexture2D _mask = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red);
    private readonly ResizableTexture2D _outline = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.DepthComponent, false);

    private readonly EmbeddedShader _combineShader = new("Framebuffers/combine.vert", "Picking/combine.frag");
    private readonly EmbeddedShader _maskShader = new("Framebuffers/combine.vert", "Picking/mask.frag");
    private readonly EmbeddedShader _outlineShader = new("Framebuffers/combine.vert", "Picking/outline.frag");
    private readonly EmbeddedShader _shader = new("Framebuffers/combine.vert", "Framebuffers/picking.frag");

    private readonly List<uint> _ids = [];
    private readonly ShaderStorageBuffer<uint> _idsBuffer = new(BufferUsageHint.DynamicDraw);
    private bool _idsDirty;

    public override void Generate()
    {
        _picking.Generate();
        _picking.Resize(Width, Height);

        _mask.Generate();
        _mask.Resize(Width, Height);
        GL.TextureParameter(_mask, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_mask, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_mask, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(_mask, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        _outline.Generate();
        _outline.Resize(Width, Height);
        GL.TextureParameter(_outline, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_outline, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_outline, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(_outline, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        _depth.Generate();
        _depth.Resize(Width, Height);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment1, _picking, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment2, _mask, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment3, _outline, 0);
        GL.NamedFramebufferRenderbuffer(Handle, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();

        _combineShader.Generate();
        _combineShader.Link();

        _maskShader.Generate();
        _maskShader.Link();

        _outlineShader.Generate();
        _outlineShader.Link();

        _shader.Generate();
        _shader.Link();

        _idsBuffer.Generate();
        _idsBuffer.Allocate(100);
    }

    public void Render()
    {
        // combine deferred and forward picking into one texture
        GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _combineShader.Use();
            _combineShader.SetUniform("deferredPicking", 0);
            _combineShader.SetUniform("forwardPicking", 1);
        });

        // use that combined texture to create a mask of the currently selected objects
        GL.DrawBuffer(DrawBufferMode.ColorAttachment2);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _maskShader.Use();
            _maskShader.SetUniform("pickingTexture", 0);
            _picking.Bind(0);

            UpdateIdsBuffer();
            _idsBuffer.Bind(3);
        });

        // use that mask to create an outline
        GL.DrawBuffer(DrawBufferMode.ColorAttachment3);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _outlineShader.Use();
            _outlineShader.SetUniform("outlineThickness", 2);
            _outlineShader.SetUniform("texelSize", new Vector2(1.0f / Width, 1.0f / Height));
            _outlineShader.SetUniform("selectionMask", 0);
            _mask.Bind(0);
        });

        // use that outline to highlight the selected object with a color
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        base.Render(() =>
        {
            _shader.Use();
            _shader.SetUniform("outlineColor", new Vector3(1.0f, 0.6f, 0.2f));
            _shader.SetUniform("outlineMask", 0);
            _outline.Bind(0);
        });
    }

    public uint ReadPixel(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        Bind();
        var pixel = 0u;

        var scaleX = windowSize.X / Width;
        var scaleY = windowSize.Y / Height;
        var x = Convert.ToInt32((mousePos.X - windowPos.X) / scaleX);
        var y = Convert.ToInt32((mousePos.Y - windowPos.Y) / scaleY);

        // ui disabled / enabled
        if (windowPos == Vector2.Zero)
            y = Height - 1 - y;
        else
            y = -y;

        // picking texture is in color attachment 1 and we the first channel of a single pixel
        GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
        GL.ReadPixels(x, y, 1, 1, PixelFormat.RedInteger, PixelType.UnsignedInt, ref pixel);
        GL.ReadBuffer(ReadBufferMode.None);

        Unbind();
        return pixel;
    }

    public void SetPickedIds(IEnumerable<uint> ids)
    {
        _ids.Clear();
        _ids.AddRange(ids.Where(id => id != 0));
        _idsDirty = true;
    }

    private void UpdateIdsBuffer()
    {
        if (!_idsDirty) return;

        var sortedIds = _ids.OrderBy(id => id).ToArray();
        var data = new uint[1 + sortedIds.Length];
        data[0] = (uint)sortedIds.Length;
        for (int i = 0; i < sortedIds.Length; i++)
        {
            data[i + 1] = sortedIds[i];
        }

        unsafe
        {
            fixed (uint* ptr = data)
            {
                _idsBuffer.Update(data.Length, (nint)ptr);
            }
        }

        _idsDirty = false;
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _mask.Resize(newWidth, newHeight);
        _outline.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public override long Allocated
    {
        get
        {
            var total = base.Allocated;
            total += _picking.Allocated;
            total += _mask.Allocated;
            total += _outline.Allocated;
            total += _depth.Allocated;
            total += _combineShader.Allocated;
            total += _maskShader.Allocated;
            total += _outlineShader.Allocated;
            total += _shader.Allocated;
            total += _idsBuffer.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            var total = base.Used;
            total += _picking.Used;
            total += _mask.Used;
            total += _outline.Used;
            total += _depth.Used;
            total += _combineShader.Used;
            total += _maskShader.Used;
            total += _outlineShader.Used;
            total += _shader.Used;
            total += _idsBuffer.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Picking Texture", _picking);
        yield return new MemoryDetail("Mask Texture", _mask);
        yield return new MemoryDetail("Outline Texture", _outline);
        yield return new MemoryDetail("Depth Renderbuffer", _depth);
        yield return new MemoryDetail("Combine Shader", _combineShader);
        yield return new MemoryDetail("Mask Shader", _maskShader);
        yield return new MemoryDetail("Outline Shader", _outlineShader);
        yield return new MemoryDetail("Main Shader", _shader);
        yield return new MemoryDetail("Picked IDs Buffer", _idsBuffer);
    }

    public override void Dispose()
    {
        base.Dispose();

        _picking.Dispose();
        _mask.Dispose();
        _outline.Dispose();
        _depth.Dispose();
        _combineShader.Dispose();
        _maskShader.Dispose();
        _outlineShader.Dispose();
        _shader.Dispose();
        _idsBuffer.Dispose();
    }
}
