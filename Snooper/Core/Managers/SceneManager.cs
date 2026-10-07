using System.Numerics;
using CUE4Parse.FileProvider;
using OpenTK.Windowing.Desktop;
using Snooper.Core.Containers;
using Snooper.Hosting;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components;
using Snooper.Rendering.Managers;
using Snooper.Rendering.Systems;

namespace Snooper.Core.Managers;

public class SceneManager : ActorManager
{
    public GameWindow Window { get; }

    public Actor? RootActor
    {
        get;
        private set
        {
            if (field == value) return;

            if (field != null) RemoveRoot(field, _endPlayReason);
            field = value;
            if (field != null) AddRoot(field);
        }
    }

    private EEndPlayReason _endPlayReason = EEndPlayReason.SceneTransition;

    public void LoadScene(Actor scene)
    {
        if (IsDisposed)
        {
            Log.Warning("Refused {Actor}, the scene is gone for good", scene.Name);
            return;
        }

        if (RootActor is { } root)
        {
            root.Children.Add(scene);
            Log.Information("{Actor} appended to {Root}", scene.Name, root.Name);
            return;
        }

        RootActor = scene;
    }

    public void UnloadScene()
    {
        if (IsDisposed) return;
        Teardown();
    }

    public readonly RenderPipeline Pipeline = new();

    protected SceneManager(GameWindow wnd, IFileProvider fileProvider) : base(fileProvider)
    {
        Window = wnd;
    }

    public override void Load()
    {
        base.Load();
        Pipeline.Generate();
    }

    public override void Update(float delta)
    {
        Bridge.Drain();

        base.Update(delta);
    }

    public override void Render()
    {
        if (GetSystem<CameraSystem>()?.Active is not { } camera) return;

        var lightSystem = Systems.Values.OfType<ClusteredLightSystem>().FirstOrDefault();

        Pipeline.RenderScene(camera, Systems.Values, lightSystem);
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

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        Pipeline.Resize(newWidth, newHeight);
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

    protected override void Teardown()
    {
        _endPlayReason = TeardownReason;
        RootActor = null; // will carry the _endPlayReason down to components and their systems

        base.Teardown();
    }

    public override void Dispose()
    {
        base.Dispose();

        ThreadManager.ClearAndDispose();
        MeshCache.ClearAndDispose();
        MaterialCache.ClearAndDispose();
        TextureCache.ClearAndDispose();

        Pipeline.Dispose();
    }
}
