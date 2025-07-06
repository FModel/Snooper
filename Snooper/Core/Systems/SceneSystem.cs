using System.Numerics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core.Containers;
using Snooper.Rendering;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers;

namespace Snooper.Core.Systems;

public sealed class SceneSystem : ActorManager, IResizable
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

    private readonly Actor? _rootActor;
    public Actor? RootActor
    {
        get => _rootActor;
        private init
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
    
    public IInterfaceSystem? InterfaceSystem { get; set; }

    private readonly NativeWindow _wnd;
    public SceneSystem(GameWindow wnd)
    {
        _wnd = wnd;
        var scene = new Actor(Guid.NewGuid(), "Scene");
        
        var grid = new Actor(Guid.NewGuid(), "Grid");
        grid.Components.Add(new GridComponent());
        scene.Children.Add(grid);

        var camera = new Actor(Guid.NewGuid(), "Camera");
        camera.Transform.Position -= Vector3.UnitZ * 5;
        camera.Transform.Position += Vector3.UnitY * 1.5f;
        camera.Components.Add(new CameraComponent());
        scene.Children.Add(camera);
        
        RootActor = scene;
    }

    public override void Load()
    {
        InterfaceSystem?.Load();

        DequeuePairs();
        base.Load();
    }

    public override void Update(float delta)
    {
        var pressed = _wnd.KeyboardState.IsKeyPressed(Keys.F10);
        if (pressed && InterfaceSystem is not null)
            InterfaceSystem.IsActive = !InterfaceSystem.IsActive;
        
        if (InterfaceSystem?.IsActive == true)
            InterfaceSystem.Update(delta);
        else if (_wnd.IsMouseButtonPressed(MouseButton.Left))
            _wnd.CursorState = CursorState.Grabbed;
        
        if (_activeCamera is null && Pairs.Count > 0)
            _activeCamera = Pairs[0].Camera;
        if (pressed && InterfaceSystem?.IsActive == false && _activeCamera is not null)
            _activeCamera.ViewportSize = new Vector2(_wnd.ClientSize.X, _wnd.ClientSize.Y);
        
        _activeCamera?.Update(_wnd.KeyboardState, delta);
        if (_wnd.CursorState == CursorState.Grabbed)
        {
            _activeCamera?.Update(_wnd.MouseState.Delta.X, _wnd.MouseState.Delta.Y);
            if (_wnd.IsMouseButtonReleased(MouseButton.Left)) _wnd.CursorState = CursorState.Normal;
        }

        DequeuePairs(1);
        base.Update(delta);
    }

    public void Render()
    {
        foreach (var pair in Pairs)
        {
            pair.DeferredRendering(Render);
            pair.ForwardRendering(Render);
            
            pair.CombineRendering();
            pair.ApplyFxaa();
        }

        if (InterfaceSystem?.IsActive == true)
        {
            InterfaceSystem.Render(this);
        }
        else if (_activeCamera is not null)
        {
            Pairs[_activeCamera.PairIndex].RenderToScreen(_wnd.ClientSize.X, _wnd.ClientSize.Y);
        }
    }

    protected override void AddComponent(ActorComponent component, Actor actor)
    {
        base.AddComponent(component, actor);

        if (component is CameraComponent cameraComponent)
        {
            _pairsToLoad.Enqueue(new CameraFramePair(cameraComponent));
        }
    }

    protected override void RemoveComponent(ActorComponent component, Actor actor)
    {
        base.RemoveComponent(component, actor);

        if (component is CameraComponent cameraComponent)
        {
            Pairs.Remove(Pairs[cameraComponent.PairIndex]);
        }
    }

    private readonly Queue<CameraFramePair> _pairsToLoad = [];
    private void DequeuePairs(int limit = 0)
    {
        for (var i = 0; i < Pairs.Count; i++)
        {
            if (Pairs[i] is { IsOpen: false, Camera.Actor: not null } pair)
            {
                _rootActor?.Children.Remove(pair.Camera.Actor);
            }
        }
        
        var count = 0;
        while (_pairsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var pair = _pairsToLoad.Dequeue();
            pair.Generate(Pairs.Count, _wnd.ClientSize.X, _wnd.ClientSize.Y);

            Pairs.Add(pair);
            count++;
        }
    }

    public void Resize(int newWidth, int newHeight)
    {
        foreach (var pair in Pairs)
            pair.Resize(newWidth, newHeight);
        
        InterfaceSystem?.Resize(newWidth, newHeight);
    }
}
