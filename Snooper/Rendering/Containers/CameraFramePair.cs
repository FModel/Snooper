using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Framebuffers;

namespace Snooper.Rendering.Containers;

public class CameraFramePair(CameraComponent camera) : IResizable, IMemoryDetailsProvider
{
    private const int DefaultWidthHeight = 1;

    public bool IsOpen = true;

    public CameraComponent Camera { get; } = camera;

    private readonly GeometryBuffer _geometry = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly SsaoFramebuffer _ssao = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ForwardFramebuffer _forward = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly CombinedFramebuffer _combined = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly FxaaFramebuffer _fxaa = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly PickingFramebuffer _picking = new(DefaultWidthHeight, DefaultWidthHeight);

    public void Generate(int pairIndex, int width, int height)
    {
        Camera.PairIndex = pairIndex;

        _geometry.Generate();
        _ssao.Generate();
        _forward.Generate();
        _combined.Generate();
        _fxaa.Generate();
        _picking.Generate();

        Resize(width, height);
    }

    public void DeferredRendering(Action<CameraComponent, ActorSystemType> render)
    {
        _geometry.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Disable(EnableCap.Blend);

        render(Camera, ActorSystemType.Deferred);

        if (Camera.bAmbientOcclusion)
        {
            _ssao.Bind();
            GL.ClearColor(1, 1, 1, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ssao.Render(shader =>
            {
                _geometry.BindTextures(true, true);
                shader.SetUniform("uProjectionMatrix", Camera.ProjectionMatrix);
                shader.SetUniform("radius", Camera.SsaoRadius);
            });
        }

        _geometry.Render(shader =>
        {
            shader.SetUniform("uLightCount", 3);
            shader.SetUniform("uLightDirs",
            [
                Vector3.TransformNormal(new Vector3(0.5f, 0.7f, 0.5f), Camera.ViewMatrix), // Key light: above and to the right
                Vector3.TransformNormal(new Vector3(-0.7f, 0.4f, 0.3f), Camera.ViewMatrix), // Fill light: softer, from left/front
                Vector3.TransformNormal(new Vector3(0.0f, 0.6f, -0.8f), Camera.ViewMatrix) // Back light: behind and above
            ]);
            shader.SetUniform("uLightColors",
            [
                new Vector3(1.0f, 0.95f, 0.85f), // Key: warm white
                new Vector3(0.6f, 0.7f, 1.0f),   // Fill: cooler tone
                new Vector3(1.0f, 1.0f, 1.0f)    // Back: neutral white
            ]);
            shader.SetUniform("uLightIntensity",
            [
                1.0f, // Key strongest
                0.5f, // Fill softer
                0.8f  // Back medium
            ]);

            shader.SetUniform("useSsao", Camera.bAmbientOcclusion);
            if (!Camera.bAmbientOcclusion) return;

            _ssao.Bind(4);
            shader.SetUniform("ssao", 4);
        });
    }

    public void ForwardRendering(Action<CameraComponent, ActorSystemType> render)
    {
        // copy depth from gBuffer
        GL.BlitNamedFramebuffer(_geometry, _forward, 0, 0, _geometry.Width, _geometry.Height, 0, 0, _forward.Width, _forward.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

        _forward.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Enable(EnableCap.Blend);

        render(Camera, ActorSystemType.Forward);
    }

    public void PickingRendering()
    {
        _picking.Bind();
        _geometry.BindPicking(0);
        _forward.BindPicking(1);
        _picking.Render();
    }

    public void CombineRendering()
    {
        _combined.Bind();
        GL.ClearColor(0.2f, 0.2f, 0.2f, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _combined.Render(_ =>
        {
            _geometry.Bind(0);
            _forward.Bind(1);
            _picking.Bind(2);
        });
    }

    public void ApplyFxaa()
    {
        if (!Camera.bFXAA) return;

        _fxaa.Bind();
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _fxaa.Render(_ => _combined.Bind(0));
    }

    public void RenderToScreen(int width, int height)
    {
        FullQuadFramebuffer framebuffer = Camera.bFXAA ? _fxaa : _combined;
        GL.BlitNamedFramebuffer(framebuffer, 0, 0, 0, framebuffer.Width, framebuffer.Height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    }

    public uint ReadPickingPixel(Vector2 mousePos, Vector2 windowPos) => _picking.ReadPixel(mousePos, windowPos, Camera.ViewportSize);
    public void SetPickedIds(IEnumerable<uint> ids) => _picking.SetPickedIds(ids);

    public void Resize(int newWidth, int newHeight)
    {
        _geometry.Resize(newWidth, newHeight);
        _ssao.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
    }

    public Texture[] GetTextures() =>
    [
        .._geometry.GetTextures(),
        .._ssao.GetTextures(),
        .._forward.GetTextures(),
        ..Camera.bFXAA ? _fxaa.GetTextures() : _combined.GetTextures(),
    ];

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _geometry.Allocated;
            total += _ssao.Allocated;
            total += _forward.Allocated;
            total += _combined.Allocated;
            total += _fxaa.Allocated;
            total += _picking.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _geometry.Used;
            total += _ssao.Used;
            total += _forward.Used;
            total += _combined.Used;
            total += _fxaa.Used;
            total += _picking.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("GBuffer", _geometry);
        yield return new MemoryDetail("SSAO", _ssao);
        yield return new MemoryDetail("Forward", _forward);
        yield return new MemoryDetail("Combined", _combined);
        yield return new MemoryDetail("FXAA", _fxaa);
        yield return new MemoryDetail("Picking", _picking);
    }
}
