using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

    public InteractiveCameraComponent? MainCamera { get; private set; }

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

    private readonly ObservableCollection<CameraComponent> _cameras = [];
    private readonly RenderPipeline _pipeline = new();

    public SceneManager(GameWindow wnd)
    {
        Window = wnd;

        _cameras.CollectionChanged += OnCamerasCollectionChanged;
    }

    public override void Load()
    {
        DequeueViewports();
        _pipeline.Generate();

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

        foreach (var viewport in Viewports)
        {
            var camera = viewport.Camera;
            _pipeline.RenderScene(camera, shadowSystems, deferredSystems, forwardSystems, directionalLight);
            _pipeline.PostProcessScene(camera, lightSystem);
            // TODO: freeze the image and send it to the viewport before processing the next viewport
        }
    }

    protected override void AddComponent(ActorComponent component, Actor actor)
    {
        base.AddComponent(component, actor);

        if (component is CameraComponent camera)
        {
            _cameras.Add(camera);
        }
    }

    protected override void RemoveComponent(ActorComponent component, Actor actor)
    {
        base.RemoveComponent(component, actor);

        if (component is CameraComponent camera)
        {
            _cameras.Remove(camera);
        }
    }

    private void OnCamerasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var component in e.NewItems!.Cast<CameraComponent>())
                {
                    if (component is InteractiveCameraComponent camera)
                    {
                        _viewportsToLoad.Enqueue(new Viewport(camera, _pipeline, Window));
                        MainCamera ??= camera;
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var component in e.OldItems!.Cast<CameraComponent>())
                {
                    var viewport = Viewports.FirstOrDefault(v => v.Camera == component);
                    if (viewport != null)
                    {
                        Viewports.Remove(viewport);
                    }

                    if (component == MainCamera)
                    {
                        MainCamera = Viewports.Select(v => v.Camera).FirstOrDefault();
                    }
                }
                break;
        }
    }

    private readonly Queue<Viewport> _viewportsToLoad = [];
    private void DequeueViewports(int limit = 0)
    {
        var count = 0;
        while (_viewportsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var viewport = _viewportsToLoad.Dequeue();
            viewport.Generate();
            viewport.Resize(Window.ClientSize.X, Window.ClientSize.Y);

            Viewports.Add(viewport);

            count++;
        }
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        foreach (var viewport in Viewports)
            viewport.Resize(newWidth, newHeight);

        _pipeline.Resize(newWidth, newHeight);
    }

    public override long Allocated
    {
        get
        {
            var total = base.Allocated;
            total += _pipeline.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            var total = base.Used;
            total += _pipeline.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Render Pipeline", _pipeline);
    }

    public override void Dispose()
    {
        base.Dispose();
        _pipeline.Dispose();

        _cameras.CollectionChanged -= OnCamerasCollectionChanged;
        _cameras.Clear();
        Viewports.Clear();
        RootActor = null;
    }
}
