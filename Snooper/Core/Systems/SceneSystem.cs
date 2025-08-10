using OpenTK.Windowing.Desktop;
using Snooper.Core.Containers;
using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers;

namespace Snooper.Core.Systems;

public class SceneSystem(GameWindow wnd) : ActorManager, IResizable
{
    protected GameWindow Window { get; } = wnd;
    protected List<CameraFramePair> Pairs { get; } = [];

    private CameraComponent? _activeCamera;
    protected CameraComponent? ActiveCamera
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
        DequeuePairs();
        base.Load();
    }

    public override void Update(float delta)
    {
        // removed closed cameras from the scene
        for (var i = 0; i < Pairs.Count; i++)
        {
            if (Pairs[i] is { IsOpen: false, Camera.Actor: not null } pair)
            {
                _rootActor?.Children.Remove(pair.Camera.Actor);
            }
        }
        
        DequeuePairs(1);
        base.Update(delta);
    }

    public virtual void Render()
    {
        foreach (var pair in Pairs)
        {
            pair.DeferredRendering(Render);
            pair.ForwardRendering(Render);
            
            pair.CombineRendering();
            pair.ApplyFxaa();
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
        var count = 0;
        while (_pairsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var pair = _pairsToLoad.Dequeue();
            pair.Generate(Pairs.Count, Window.ClientSize.X, Window.ClientSize.Y);

            Pairs.Add(pair);
            count++;
        }
    }

    public virtual void Resize(int newWidth, int newHeight)
    {
        foreach (var pair in Pairs)
            pair.Resize(newWidth, newHeight);
    }

    public override void Dispose()
    {
        RootActor = null;
        base.Dispose();
    }
}
