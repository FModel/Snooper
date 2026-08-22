using Serilog;
using Serilog.Core;
using System.Reflection;
using CUE4Parse.FileProvider;
using ImGuiNET;
using Snooper.Core.Containers;
using Snooper.Core.Hardware;
using Snooper.Core.Systems;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Core.Managers;

public abstract class ActorManager(IFileProvider fileProvider) : IGameSystem, IMemoryDetailsProvider, IControllable, IResizable
{
    private static Func<ActorSystem, bool> IsSystemNotOfType(Type type) => x => x.GetType() != type;

    public uint FragmentColor = FragmentColorMode.Disabled;
    public int ActorCount { get; private set; }
    public uint Revision { get; private set; }
    public float Time { get; private set; }
    public RendererInfo Renderer { get; } = new();
    public ThreadManager ThreadManager { get; } = new(Environment.ProcessorCount - 2);
    public IFileProvider FileProvider { get; } = fileProvider;
    protected SortedList<uint, ActorSystem> Systems { get; } = [];

    protected ILogger Log => field ??= Serilog.Log.ForContext(Constants.SourceContextPropertyName, GetType().Name);

    public virtual void Load()
    {
        Renderer.Load();
        DequeueSystems();
    }

    public virtual void Update(float delta)
    {
        Time += delta;

        Renderer.Update(delta);
        DequeueSystems(1);
        TextureCache.Update();
        TrackBackgroundWork();

        foreach (var system in Systems.Values)
        {
            system.Update(delta);
        }
    }

    public abstract void Render();

    protected void AddRoot(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new ArgumentException("This actor should not have a parent.", nameof(actor));
        }
        if (actor.ActorManager != null)
        {
            throw new ArgumentException("This actor is already used by another actor manager.", nameof(actor));
        }

        actor.SetScene(this, EEndPlayReason.Destroyed);
        Log.Information("{Actor} brought {ActorCount} actors into the scene", actor.Name, ActorCount);
    }

    protected void RemoveRoot(Actor actor, EEndPlayReason reason = EEndPlayReason.Destroyed)
    {
        if (actor.Parent != null)
        {
            throw new ArgumentException("This actor should not have a parent.", nameof(actor));
        }
        if (actor.ActorManager != this)
        {
            throw new ArgumentException("This actor is not part of this actor manager.", nameof(actor));
        }

        var before = ActorCount;
        actor.SetScene(null, reason);
        Log.Information("{Actor} took {ActorCount} actors out of the scene ({Reason})", actor.Name, before - ActorCount, reason);
    }

    internal void RegisterActor(Actor actor)
    {
        ActorCount++;
        IncrementRevision();

        Log.Verbose("{Actor} entered the scene", actor.Name);
    }

    internal void UnregisterActor(Actor actor)
    {
        ActorCount--;
        IncrementRevision();

        Log.Verbose("{Actor} left the scene", actor.Name);
    }

    internal void IncrementRevision() => Revision++;

    internal void RegisterComponent(ActorComponent component)
    {
        var actor = component.Actor!;

        Log.Verbose("Offering {Component} of {Actor} to the systems", component.Name, actor.Name);
        AddComponent(component, actor);
    }

    internal void UnregisterComponent(ActorComponent component, Actor actor, EEndPlayReason reason)
    {
        Log.Verbose("Taking {Component} of {Actor} back from the systems ({Reason})", component.Name, actor.Name, reason);
        RemoveComponent(component, actor, reason);
    }

    protected virtual void AddComponent(ActorComponent component, Actor actor)
    {
        foreach (var system in SystemsFor(component.GetType(), true))
        {
            system.RegisterComponent(component, actor);
        }
    }

    protected virtual void RemoveComponent(ActorComponent component, Actor actor, EEndPlayReason reason)
    {
        foreach (var system in SystemsFor(component.GetType(), false))
        {
            system.UnregisterComponent(component, actor, reason);
        }
    }

    private readonly Dictionary<Type, List<ActorSystem>> _systemsPerComponentType = [];
    private List<ActorSystem> SystemsFor(Type componentType, bool collectNew)
    {
        if (_systemsPerComponentType.TryGetValue(componentType, out var systemsForComponent))
            return systemsForComponent;

        if (collectNew)
        {
            CollectNewActorSystems(componentType);
        }

        systemsForComponent = [];
        foreach (var system in _systemsToLoad) AddIfAccepted(system);
        foreach (var system in Systems.Values) AddIfAccepted(system);
        _systemsPerComponentType.Add(componentType, systemsForComponent);

        return systemsForComponent;

        void AddIfAccepted(ActorSystem system)
        {
            if (system.Accepts(componentType))
            {
                systemsForComponent.Add(system);
            }
        }
    }

    private void CollectNewActorSystems(Type componentType)
    {
        var actorSystemAttributes = componentType.GetCustomAttributes<DefaultActorSystemAttribute>();
        foreach (var actorSystemAttribute in actorSystemAttributes)
        {
            var addNewSystem = _systemsToLoad.All(IsSystemNotOfType(actorSystemAttribute.Type)) && Systems.Values.All(IsSystemNotOfType(actorSystemAttribute.Type));
            if (!addNewSystem) continue;

            if (actorSystemAttribute.Type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"{actorSystemAttribute.Type.Name} must have a parameterless constructor");

            var system = (ActorSystem)Activator.CreateInstance(actorSystemAttribute.Type)!;
            system.ActorManager = this;
            _systemsToLoad.Enqueue(system);
        }
    }

    private readonly Queue<ActorSystem> _systemsToLoad = [];
    private void DequeueSystems(int limit = 0)
    {
        var count = 0;
        while (_systemsToLoad.Count > 0 && (limit == 0 || count < limit))
        {
            var system = _systemsToLoad.Dequeue();
            if (system.EnqueuedComponentsCount == 0)
            {
                system.Dispose();
                continue;
            }

            Systems.Add(system.Order, system);
            system.Load();
            count++;
        }
    }

    private const int MinBackgroundWork = 8;

    private int _peakQueuedJobs;
    private int _peakPendingTextures;
    private void TrackBackgroundWork()
    {
        var jobs = ThreadManager.CurrentQueuedJobs;
        if (jobs > 0)
        {
            _peakQueuedJobs = Math.Max(_peakQueuedJobs, jobs);
        }
        else if (_peakQueuedJobs > 0)
        {
            if (_peakQueuedJobs >= MinBackgroundWork)
            {
                Notifications.Push("work.jobs", Settings.JobIcon, $"{_peakQueuedJobs:N0} jobs processed");
            }

            _peakQueuedJobs = 0;
        }

        var textures = TextureCache.PendingTextureCount;
        if (textures > 0)
        {
            _peakPendingTextures = Math.Max(_peakPendingTextures, textures);
        }
        else if (_peakPendingTextures > 0)
        {
            if (_peakPendingTextures >= MinBackgroundWork)
            {
                Notifications.Push("work.textures", Settings.TextureIcon, $"{_peakPendingTextures:N0} textures uploaded");
            }

            _peakPendingTextures = 0;
        }
    }

    public virtual void Resize(int newWidth, int newHeight)
    {
        foreach (var system in Systems.Values.OfType<IResizable>())
            system.Resize(newWidth, newHeight);
    }

    public T? GetSystem<T>() where T : ActorSystem => GetSystems<T>().FirstOrDefault();
    public IEnumerable<T> GetSystems<T>() where T : ActorSystem => GetSystemsInternal<T>();
    internal IEnumerable<T> GetSystemsInternal<T>() => Systems.Values.OfType<T>();

    public virtual void DrawControls()
    {
        EditorUI.Caption($"API: {Renderer.Name} | GPU: {Renderer.DeviceInfo.Name}");

        ImGui.SeparatorText("General");

        ImGui.TextUnformatted("Fragment Color");
        EditorUI.FragmentColorCombo("##FragmentColor", ref FragmentColor);

        var light = Systems.Values.OfType<ClusteredLightSystem>().FirstOrDefault();
        ImGui.BeginDisabled(light == null);
        EditorUI.TogglableTreeNode("Lighting", light?.IsEnabled ?? false, () => light?.DrawControls(), toggle =>
        {
            light?.IsEnabled = toggle;
            light?.DirectionalLight?.IsEnabled = !toggle;
            // TODO: auto disable shadows
        });
        ImGui.EndDisabled();

        var audio = Systems.Values.OfType<AudioSystem>().FirstOrDefault();
        ImGui.BeginDisabled(audio == null);
        EditorUI.TogglableTreeNode("Audio", audio?.IsEnabled ?? false, () => audio?.DrawControls(), toggle => audio?.IsEnabled = toggle);
        ImGui.EndDisabled();

        var landscape = Systems.Values.OfType<LandscapeSystem>().FirstOrDefault();
        ImGui.BeginDisabled(landscape == null);
        EditorUI.TogglableTreeNode("Landscape", landscape?.IsEnabled ?? false, () => landscape?.DrawControls(), toggle => landscape?.IsEnabled = toggle);
        ImGui.EndDisabled();

        var debug = Systems.Values.OfType<DebugSystem>().FirstOrDefault();
        ImGui.BeginDisabled(debug == null);
        EditorUI.TogglableTreeNode("Wireframes", debug?.IsEnabled ?? false, () => debug?.DrawControls(), toggle => debug?.IsEnabled = toggle);
        ImGui.EndDisabled();
    }

    public bool IsDisposed { get; private set; }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; // it is what makes the teardown below a Shutdown one

        Teardown();
        ThreadManager.Dispose();
    }

    protected EEndPlayReason TeardownReason => IsDisposed ? EEndPlayReason.Shutdown : EEndPlayReason.SceneTransition;

    protected virtual void Teardown()
    {
        while (_systemsToLoad.Count > 0)
        {
            _systemsToLoad.Dequeue().Dispose();
        }

        foreach (var system in Systems.Values.ToArray())
        {
            system.Dispose();
        }

        Systems.Clear();
        _systemsPerComponentType.Clear();
    }

    public virtual long Allocated
    {
        get
        {
            long total = TextureCache.Allocated;
            foreach (var system in Systems.Values)
            {
                if (system is IMemorySizeProvider provider)
                    total += provider.Allocated;
            }
            return total;
        }
    }

    public virtual long Used
    {
        get
        {
            long total = TextureCache.Used;
            foreach (var system in Systems.Values)
            {
                if (system is IMemorySizeProvider provider)
                    total += provider.Used;
            }
            return total;
        }
    }

    public virtual IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var system in Systems.Values)
        {
            switch (system)
            {
                case IMemoryDetailsProvider provider:
                    yield return new MemoryDetail(system.GetType().Name, "ActorSystem", provider);
                    break;
                case IMemorySizeProvider sizeProvider:
                    yield return new MemoryDetail(system.GetType().Name, "ActorSystem", sizeProvider);
                    break;
            }
        }
    }
}
