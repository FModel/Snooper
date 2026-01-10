using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.Rendering.Systems;

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
    private readonly ShadowFramebuffer _shadow = new(2048, 2048);

    private bool _updateShadows = true;
    private Matrix4x4 _shadowViewMatrix = Matrix4x4.CreateLookAt(new Vector3(10, 10, -10), Vector3.Zero, Vector3.UnitZ);
    private Matrix4x4 _shadowProjectionMatrix = Matrix4x4.CreateOrthographic(25, 25, 1.0f, 200.0f);
    private Matrix4x4 _lastOrthoViewProjectionMatrix = Matrix4x4.Identity;

    public void Generate(int pairIndex, int width, int height)
    {
        Camera.PairIndex = pairIndex;
        Camera.OnRequestSystemUpdate += component =>
        {
            if (component is CameraComponent { bOrthographic: true })
            {
                _updateShadows = true;
            }
        };
        TransformSystem.OnTransformComponentUpdated += component =>
        {
            if (component is not CameraComponent)
            {
                _updateShadows = true;
            }
        };

        _geometry.Generate();
        _ssao.Generate();
        _forward.Generate();
        _combined.Generate();
        _fxaa.Generate();
        _picking.Generate();
        _shadow.Generate();

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
            // Calculate sun direction from shadow view matrix to match shadow direction
            // Extract the forward vector (third column) - this is the direction FROM the light
            Vector3 sunDirection = Vector3.Normalize(new Vector3(
                _shadowViewMatrix.M13,
                _shadowViewMatrix.M23,
                _shadowViewMatrix.M33
            ));

            shader.SetUniform("uSunDirection", sunDirection);
            shader.SetUniform("uSunColor", new Vector3(1.0f, 0.98f, 0.95f));
            shader.SetUniform("uSunIntensity", 3.5f);

            Matrix4x4.Invert(Camera.ViewMatrix, out var inverseViewMatrix);
            shader.SetUniform("uInverseViewMatrix", inverseViewMatrix);

            shader.SetUniform("useSsao", Camera.bAmbientOcclusion);
            if (Camera.bAmbientOcclusion)
            {
                _ssao.Bind(4);
                shader.SetUniform("ssao", 4);
            }

            shader.SetUniform("useShadows", Camera.bShadows);
            if (Camera.bShadows)
            {
                shader.SetUniform("uLightViewProjectionMatrix", _lastOrthoViewProjectionMatrix);
                _shadow.Bind(5);
                shader.SetUniform("shadowMap", 5);
            }
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

    public void ShadowRendering(Action<CameraComponent> render)
    {
        if (!Camera.bShadows || !_updateShadows) return;

        _shadow.Bind();
        GL.Clear(ClearBufferMask.DepthBufferBit);

        if (Camera.bOrthographic)
        {
            _shadowViewMatrix = Camera.ViewMatrix;
            _shadowProjectionMatrix = Camera.ProjectionMatrix;
        }

        // Create a temporary camera component for shadow rendering
        var shadowCamera = new CameraComponent
        {
            ViewMatrix = _shadowViewMatrix,
            ProjectionMatrix = _shadowProjectionMatrix,
            ViewProjectionMatrix = _shadowViewMatrix * _shadowProjectionMatrix
        };
        _lastOrthoViewProjectionMatrix = shadowCamera.ViewProjectionMatrix;

        render(shadowCamera);

        _updateShadows = false;
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
        .._shadow.GetTextures(),
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
            total += _shadow.Allocated;
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
            total += _shadow.Used;
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
        yield return new MemoryDetail("Shadow", _shadow);
    }
}
