using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Buffers;

namespace Snooper.Rendering.Containers;

public class CameraFramePair(CameraComponent camera) : IResizable
{
    private const int DefaultWidthHeight = 1;

    public CameraComponent Camera { get; } = camera;

    private readonly GeometryBuffer _geometry = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly SsaoFramebuffer _ssao = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ForwardFramebuffer _forward = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly CombinedFramebuffer _combined = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly FxaaFramebuffer _fxaa = new(DefaultWidthHeight, DefaultWidthHeight);

    public void Generate(int width, int height)
    {
        _geometry.Generate();
        _ssao.Generate();
        _forward.Generate();
        _combined.Generate();
        _fxaa.Generate();

        Resize(width, height);
    }

    public void DeferredRendering(Action<CameraComponent, ActorSystemType> render)
    {
        _geometry.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Disable(EnableCap.Blend);

        render(Camera, ActorSystemType.Deferred);

        if (Camera.bSSAO)
        {
            _ssao.Bind();
            GL.ClearColor(1, 1, 1, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ssao.Render(shader =>
            {
                _geometry.BindTextures(true, true, false);
                shader.SetUniform("uProjectionMatrix", Camera.ProjectionMatrix);
                shader.SetUniform("radius", Camera.SsaoRadius);
                shader.SetUniform("bias", Camera.SsaoBias);
            });
        }

        _geometry.Render(shader =>
        {
            if (!Camera.bSSAO) return;
            _ssao.Bind(TextureUnit.Texture3);
            shader.SetUniform("useSsao", true);
            shader.SetUniform("ssao", 3);
        });
    }

    public void ForwardRendering(Action<CameraComponent, ActorSystemType> render)
    {
        if (_geometry != GL.GetInteger(GetPName.ReadFramebufferBinding))
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _geometry);

        // copy depth from gBuffer
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _forward);
        GL.BlitFramebuffer(0, 0, _geometry.Width, _geometry.Height, 0, 0, _forward.Width, _forward.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

        _forward.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Enable(EnableCap.Blend);
        
        render(Camera, ActorSystemType.Forward);
    }

    public void CombineRendering()
    {
        _combined.Bind();
        GL.ClearColor(0.66f, 0.88f, 0.44f, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        
        _combined.Render(_ =>
        {
            _geometry.Bind(TextureUnit.Texture0);
            _forward.Bind(TextureUnit.Texture1);
        });
    }
    
    public void ApplyFxaa()
    {
        if (!Camera.bFXAA) return;
        
        _fxaa.Bind();
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _fxaa.Render(_ => _combined.Bind(TextureUnit.Texture0));
    }

    public void Resize(int newWidth, int newHeight)
    {
        _geometry.Resize(newWidth, newHeight);
        _ssao.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
    }

    public IntPtr[] GetPointers() =>
    [
        .._geometry.GetTexturePointers(),
        _ssao.GetPointer(),
        _forward.GetPointer(),
        Camera.bFXAA ? _fxaa.GetPointer() : _combined.GetPointer(),
    ];
}
