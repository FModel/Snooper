using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers;
using Snooper.Rendering.Containers.Buffers;

namespace Snooper.Core.Systems;

public sealed class SceneSystem(GameWindow wnd) : ActorManager
{
    public List<CameraFramePair> Pairs { get; } = [];

    private CameraComponent? _activeCamera;
    public CameraComponent? ActiveCamera
    {
        get => _activeCamera;
        set
        {
            if (_activeCamera == value)
                return;

            if (_activeCamera != null)
                _activeCamera.IsActive = false;

            if (value != null)
                value.IsActive = true;

            _activeCamera = value;
        }
    }

    private Actor? _rootActor;
    public Actor? RootActor
    {
        get => _rootActor;
        set
        {
            if (_rootActor == value)
                return;

            if (_rootActor != null)
                RemoveRoot(_rootActor);

            if (value != null)
                AddRoot(value);

            _rootActor = value;
        }
    }

    public override void Load()
    {
        foreach (var pair in Pairs)
        {
            pair.Generate(wnd.ClientSize.X, wnd.ClientSize.Y);
        }

        base.Load();
    }

    public override void Update(float delta)
    {
        _activeCamera?.Update(wnd.KeyboardState, delta);

        base.Update(delta);
    }

    public void Render()
    {
        foreach (var pair in Pairs)
        {
            // render gBuffer
            pair.GBuffer.Bind();
            Render(pair.Camera, ActorSystemType.DeferredRender);
            pair.GBuffer.Render();

            // copy gColor to framebuffer
            // GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pair.GBuffer);
            // GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pair.Framebuffer);
            // GL.BlitFramebuffer(0, 0, pair.GBuffer.Width, pair.GBuffer.Height, 0, 0, pair.Framebuffer.Width, pair.Framebuffer.Height, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            // pair.Framebuffer.Render(pair.GBuffer.Shade);

            // // copy depth from gBuffer to msaaBuffer
            // GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pair.MsaaBuffer);
            // GL.BlitFramebuffer(0, 0, pair.GBuffer.Width, pair.GBuffer.Height, 0, 0, pair.MsaaBuffer.Width, pair.MsaaBuffer.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

            // render msaaBuffer
            pair.MsaaBuffer.Bind();
            Render(pair.Camera, ActorSystemType.ForwardRender);
            pair.MsaaBuffer.Render();

            // // // copy msaaBuffer to framebuffer
            // GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pair.MsaaBuffer);
            // GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pair.Framebuffer);
            // GL.BlitFramebuffer(0, 0, pair.MsaaBuffer.Width, pair.MsaaBuffer.Height, 0, 0, pair.Framebuffer.Width, pair.Framebuffer.Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            // pair.Framebuffer.Render(pair.MsaaBuffer.Shade);
        }
    }

    protected override void AddComponent(ActorComponent component, Actor actor)
    {
        base.AddComponent(component, actor);

        if (component is CameraComponent cameraComponent)
        {
            Pairs.Add(new CameraFramePair(cameraComponent));
        }
    }

    protected override void RemoveComponent(ActorComponent component, Actor actor)
    {
        base.RemoveComponent(component, actor);

        if (component is CameraComponent cameraComponent && Pairs.Find(x => x.Camera == cameraComponent) is var camera)
        {
            Pairs.Remove(camera);
        }
    }
}
