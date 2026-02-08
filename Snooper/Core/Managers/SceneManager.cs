using System.Collections.ObjectModel;
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
    protected GameWindow Window { get; }
    protected Viewport? MainViewport { get; private set; }

    public Actor? RootActor
    {
        get;
        set
        {
            if (field == value) return;

            if (field != null) RemoveRoot(field);
            field = value;
            if (field != null) AddRoot(field);
        }
    }

    protected readonly ObservableCollection<Viewport> Viewports = [];
    protected readonly RenderPipeline Pipeline = new();

    private readonly HashSet<CameraComponent> _cameras = [];

    protected SceneManager(GameWindow wnd)
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
        base.Update(delta);
    }

    public override void Render()
    {
        var shadowSystems = Systems.Values.OfType<IShadowSystem>().ToArray();
        var lightSystem = Systems.Values.OfType<ClusteredLightSystem>().FirstOrDefault();
        var directionalLight = lightSystem?.GetDirectionalLight();

        var deferredSystems = Systems.Values.Where(x => x.SystemType == ActorSystemType.Deferred).ToArray();
        var forwardSystems = Systems.Values.Where(x => x.SystemType == ActorSystemType.Forward).ToArray();

        // TODO: we do not support multiple cameras yet
        if (MainViewport != null)
        {
            var camera = MainViewport.Camera;
            Pipeline.RenderScene(camera, shadowSystems, deferredSystems, forwardSystems, directionalLight);
            Pipeline.PostProcessScene(camera, lightSystem);
        }
    }

    protected override void AddComponent(ActorComponent component, Actor actor)
    {
        base.AddComponent(component, actor);

        if (component is CameraComponent camera)
        {
            _cameras.Add(camera);

            if (camera is InteractiveCameraComponent interactiveCamera)
            {
                _viewportsToLoad.Enqueue(new Viewport(interactiveCamera, Pipeline, Window));
            }
        }
    }

    protected override void RemoveComponent(ActorComponent component, Actor actor)
    {
        base.RemoveComponent(component, actor);

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
        base.Dispose();
        Pipeline.Dispose();

        _cameras.Clear();
        Viewports.Clear();
        RootActor = null;
    }
}
