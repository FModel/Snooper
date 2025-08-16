using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Framebuffers;

namespace Snooper.Rendering.Containers;

public class CameraFramePair(CameraComponent camera) : IResizable
{
    private const int DefaultWidthHeight = 1;

    public bool IsOpen = true;

    public CameraComponent Camera { get; } = camera;

    private readonly GeometryBuffer _geometry = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly SsaoFramebuffer _ssao = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ForwardFramebuffer _forward = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly CombinedFramebuffer _combined = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly FxaaFramebuffer _fxaa = new(DefaultWidthHeight, DefaultWidthHeight);

    public void Generate(int pairIndex, int width, int height)
    {
        Camera.PairIndex = pairIndex;
        
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
            
            _ssao.Bind(TextureUnit.Texture4);
            shader.SetUniform("ssao", 4);
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
        GL.ClearColor(0.2f, 0.2f, 0.2f, 1);
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

    public void RenderToScreen(int width, int height)
    {
        FullQuadFramebuffer framebuffer = Camera.bFXAA ? _fxaa : _combined;
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, framebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        GL.BlitFramebuffer(0, 0, framebuffer.Width, framebuffer.Height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    }

    public void Resize(int newWidth, int newHeight)
    {
        _geometry.Resize(newWidth, newHeight);
        _ssao.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
    }

    public IntPtr[] GetFramebuffers() =>
    [
        .._geometry.GetTexturePointers(),
        _ssao.GetPointer(),
        _forward.GetPointer(),
        Camera.bFXAA ? _fxaa.GetPointer() : _combined.GetPointer(),
    ];
}
