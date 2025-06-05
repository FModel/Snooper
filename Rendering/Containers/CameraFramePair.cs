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
    private readonly FxaaFramebuffer _fxaa = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly PostProcessingFramebuffer _framebuffer = new(DefaultWidthHeight, DefaultWidthHeight);

    public void Generate(int width, int height)
    {
        _geometry.Generate();
        _ssao.Generate();
        _fxaa.Generate();
        _framebuffer.Generate();

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
            GL.ClearColor(255, 255, 255, 255);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ssao.Render(shader =>
            {
                _geometry.BindTextures(true, true, false);
                shader.SetUniform("uViewMatrix", Camera.ViewMatrix);
                shader.SetUniform("uProjectionMatrix", Camera.ProjectionMatrix);
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
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fxaa);
        GL.BlitFramebuffer(0, 0, _geometry.Width, _geometry.Height, 0, 0, _fxaa.Width, _fxaa.Height,
            ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

        _fxaa.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Enable(EnableCap.Blend);

        render(Camera, ActorSystemType.Forward);

        _fxaa.Render();
    }

    public void PostProcessingRendering(Action<CameraComponent, ActorSystemType> render)
    {
        _framebuffer.Bind();
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        render(Camera, ActorSystemType.Background);

        _geometry.Bind(TextureUnit.Texture0);
        _fxaa.Bind(TextureUnit.Texture1);
        _framebuffer.Render();
    }

    public void Resize(int newWidth, int newHeight)
    {
        _geometry.Resize(newWidth, newHeight);
        _ssao.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
        _framebuffer.Resize(newWidth, newHeight);
    }

    public IntPtr GetPointer() => _framebuffer.GetPointer();
    public IntPtr[] GetPointers() =>
    [
        _geometry.GetPointer(),
        _ssao.GetPointer(),
        _fxaa.GetPointer(),
        _framebuffer.GetPointer()
    ];
}
