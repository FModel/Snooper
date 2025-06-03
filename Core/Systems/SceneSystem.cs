using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers;

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
            pair.Framebuffer.Generate();
            pair.Framebuffer.Resize(wnd.ClientSize.X, wnd.ClientSize.Y);
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
        GL.ClearColor(OpenTK.Mathematics.Color4.DarkOliveGreen);
        foreach (var pair in Pairs)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, pair.Framebuffer);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            Render(pair.Camera);
            pair.Framebuffer.RenderPostProcessing();
        }
    }

    protected override void AddComponent(ActorComponent component, Actor actor)
    {
        base.AddComponent(component, actor);

        if (component is CameraComponent cameraComponent)
        {
            Pairs.Add(new CameraFramePair(new MsaaFramebuffer(1, 1), cameraComponent));
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
