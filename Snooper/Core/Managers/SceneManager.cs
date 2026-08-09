using System.Collections.ObjectModel;
using System.Numerics;
using CUE4Parse.FileProvider;
using OpenTK.Windowing.Desktop;
using Snooper.Core.Containers;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Managers;
using Snooper.Rendering.Systems;

namespace Snooper.Core.Managers;

public class SceneManager : ActorManager
{
    public GameWindow Window { get; }
    public Viewport? MainViewport { get; private set; }

    public Actor? RootActor
    {
        get;
        set
        {
            if (field == value) return;

            if (field != null) RemoveRoot(field, _endPlayReason);
            field = value;
            if (field != null) AddRoot(field);
        }
    }

    private EEndPlayReason _endPlayReason = EEndPlayReason.SceneTransition;

    protected readonly ObservableCollection<Viewport> Viewports = [];
    public readonly RenderPipeline Pipeline = new();

    private readonly HashSet<CameraComponent> _cameras = [];

    protected SceneManager(GameWindow wnd, IFileProvider fileProvider) : base(fileProvider)
    {
        Window = wnd;
    }

    public override void Load()
    {
        DequeueViewports();
        Pipeline.Generate();

        base.Load();
    }

    public override void Update(float delta)
    {
        DequeueViewports(1);

        if (RootActor != null && MainViewport?.Camera is { } camera && camera.IsDirty(DirtyFlags.Transform))
        {
            UpdatePartitionActorsRecursive(RootActor, camera.LocalTransform.Position);
        }

        base.Update(delta);
    }

    public override void Render()
    {
        // TODO: we do not support multiple cameras yet
        if (MainViewport == null) return;

        var camera = MainViewport.Camera;
        var lightSystem = Systems.Values.OfType<ClusteredLightSystem>().FirstOrDefault();

        Pipeline.RenderScene(camera, Systems.Values, lightSystem?.DirectionalLight);
        Pipeline.PostProcessScene(camera, lightSystem);
    }

    protected uint GetComponentId(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize) => Pipeline.GetComponentId(mousePos, windowPos, windowSize);

    protected ActorComponent? GetComponentById(uint id)
    {
        if (id == 0 || RootActor == null)
            return null;

        return FindRecursive(RootActor);

        ActorComponent? FindRecursive(Actor actor)
        {
            foreach (var component in actor.Components)
            {
                if (component.Id == id)
                {
                    return component;
                }
            }

            foreach (var child in actor.Children)
            {
                var found = FindRecursive(child);
                if (found != null)
                    return found;
            }

            return null;
        }
    }

    private void UpdatePartitionActorsRecursive(Actor actor, Vector3 cameraPosition)
    {
        if (actor is PartitionActor partition)
        {
            partition.SetVisibilityByDistance(cameraPosition);
        }
        else foreach (var child in actor.Children)
        {
            UpdatePartitionActorsRecursive(child, cameraPosition);
        }
    }

    protected sealed override void AddComponent(ActorComponent component, Actor actor)
    {
        base.AddComponent(component, actor);

        if (component is CameraComponent camera)
        {
            _cameras.Add(camera);

            if (camera is InteractiveCameraComponent interactiveCamera)
            {
                _viewportsToLoad.Enqueue(new Viewport(interactiveCamera));
            }
        }
    }

    protected sealed override void RemoveComponent(ActorComponent component, Actor actor, EEndPlayReason reason)
    {
        base.RemoveComponent(component, actor, reason);

        if (component is CameraComponent camera)
        {
            _cameras.Remove(camera);

            if (camera is InteractiveCameraComponent interactiveCamera)
            {
                var viewport = Viewports.FirstOrDefault(v => v.Camera == interactiveCamera);
                if (viewport != null)
                {
                    Viewports.Remove(viewport);
                    if (MainViewport == viewport)
                    {
                        MainViewport = Viewports.FirstOrDefault();
                    }
                }
            }
        }
    }

    private readonly Queue<Viewport> _viewportsToLoad = [];
    private void DequeueViewports(int limit = 0)
    {
        var count = 0;
        while (_viewportsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var viewport = _viewportsToLoad.Dequeue();
            viewport.Resize(Window.ClientSize.X, Window.ClientSize.Y);

            Viewports.Add(viewport);
            MainViewport ??= viewport;

            count++;
        }
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        foreach (var viewport in Viewports)
            viewport.Resize(newWidth, newHeight);

        Pipeline.Resize(newWidth, newHeight);
    }

    public override void DrawControls()
    {
        base.DrawControls();

        Pipeline.DrawControls();

        MainViewport?.DrawControls();
    }

    public override long Allocated
    {
        get
        {
            var total = base.Allocated;
            total += Pipeline.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            var total = base.Used;
            total += Pipeline.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Render Pipeline", Pipeline);
    }

    public override void Dispose()
    {
        _endPlayReason = EEndPlayReason.Shutdown;
        RootActor = null;

        base.Dispose();
        Pipeline.Dispose();

        _cameras.Clear();
        Viewports.Clear();
    }
}
