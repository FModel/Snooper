using Serilog;
using Serilog.Core;
using System.Reflection;
using CUE4Parse.FileProvider;
using ImGuiNET;
using Snooper.Core.Containers;
using Snooper.Core.Hardware;
using Snooper.Core.Systems;
using Snooper.Hosting;
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
    public readonly WireframeOptions Wireframe = new();

    public int ActorCount { get; private set; }
    public uint Revision { get; private set; }
    public float Time { get; private set; }
    public RendererInfo Renderer { get; } = new();
    public FrameBudget Budget { get; } = new();
    public IFileProvider FileProvider { get; } = fileProvider;
    protected SortedList<uint, ActorSystem> Systems { get; } = [];
    internal StreamingQueue Streaming { get; } = new();

    protected ILogger Log => field ??= Serilog.Log.ForContext(Constants.SourceContextPropertyName, GetType().Name);

    public virtual void Load()
    {
        Renderer.Load();
        DequeueSystems();
    }

    public virtual void Update(float delta)
    {
        Time += delta;
        Budget.Begin();

        using (Profiler.Cpu("Streaming"))
        {
            Streaming.Update(Budget);
        }

        using (Profiler.Cpu("Entering"))
        {
            EnterActors();
        }

        Renderer.Update(delta);
        DequeueSystems(Budget);

        foreach (var system in Systems.Values)
        {
            system.Update(delta);
        }

        TextureCache.Update(Budget);
        ThreadManager.Update();
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
        Log.Information("{Actor} is the root of the scene", actor.Name);
    }

    private const float EnterShare = 0.5f;
    private const int MinReportedActors = 256;

    private readonly Queue<Actor> _entering = new();
    private int _enteringTotal;
    private int _enteringDone;

    internal void Enter(IEnumerable<Actor> children)
    {
        foreach (var child in children)
        {
            _entering.Enqueue(child);
            _enteringTotal++;
        }
    }

    private void EnterActors()
    {
        if (_enteringTotal == 0) return;

        var entered = 0;
        while (_entering.Count > 0 && (entered == 0 || !Budget.Spent(EnterShare)))
        {
            var actor = _entering.Dequeue();
            _enteringDone++;

            if (actor.Parent?.ActorManager != this) continue; // it was detached, or its parent left, before its turn

            actor.SetScene(this, EEndPlayReason.Destroyed);
            entered++;
        }

        var finished = _entering.Count == 0;
        if (_enteringTotal >= MinReportedActors)
        {
            var completed = finished ? $"{_enteringTotal:N0} actors entered the scene" : null;
            Progress.Report("work.actors", Settings.CubeIcon, "Entering the scene", _enteringDone, _enteringTotal, completed);
        }

        if (!finished) return;

        Log.Information("{Count} actors entered the scene, {ActorCount} in total", _enteringTotal, ActorCount);
        _enteringTotal = _enteringDone = 0;
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

        if (actor is StreamableActor streamable && (streamable.IsPersistent || streamable.Wanted))
            streamable.Load();

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
    private void DequeueSystems(FrameBudget? budget = null)
    {
        var count = 0;
        while (_systemsToLoad.Count > 0 && (count == 0 || budget?.Exhausted != true))
        {
            var system = _systemsToLoad.Dequeue();
            if (system.EnqueuedComponentsCount == 0)
            {
                system.Dispose();
                continue;
            }

            Systems.Add(system.Order, system);
            system.Load();

            if (system is IResizable resizable)
                resizable.Resize(_width, _height); // resize right away for the screen-sized resources to get allocated (ClusteredLightSystem)

            count++;
        }
    }

    private int _width;
    private int _height;
    public virtual void Resize(int newWidth, int newHeight)
    {
        _width = newWidth;
        _height = newHeight;

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
        EditorUI.TogglableTreeNode("Lighting", light?.UseSceneLights ?? false, () => light?.DrawControls(), toggle =>
        {
            light?.UseSceneLights = toggle;
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
        Streaming.Clear();
        _entering.Clear();
        _enteringTotal = _enteringDone = 0;

        Progress.Clear();
        WindowRequests.Clear();
        TextureCache.Clear();
        Bridge.Clear();
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
