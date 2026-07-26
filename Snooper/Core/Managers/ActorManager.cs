using System.Collections.Specialized;
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

    private readonly Dictionary<Type, List<ActorSystem>> _systemsPerComponentType = [];
    private readonly HashSet<int> _actors = [];

    public uint FragmentColor = FragmentColorMode.Disabled;

    public float Time { get; private set; }
    public RendererInfo Renderer { get; } = new();
    public ThreadManager ThreadManager { get; } = new(Environment.ProcessorCount - 2);
    public IFileProvider FileProvider { get; } = fileProvider;
    protected SortedList<uint, ActorSystem> Systems { get; } = [];

    public int ActorCount => _actors.Count;

    public virtual void Load()
    {
        Renderer.Load();
        DequeueSystems();
    }

    public virtual void Update(float delta)
    {
        Time += delta;

        DequeueSystems(1);
        TextureCache.Update();

        using (Profiler.Cpu("Update"))
        {
            foreach (var system in Systems.Values)
            {
                system.Update(delta);
            }
        }
    }

    public abstract void Render();

    protected void AddRoot(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new ArgumentException("This actor should not have a parent.", nameof(actor));
        }

        AddActorInternal(actor);
    }
    private void AddActorInternal(Actor actor)
    {
        if (!_actors.Add(actor.Id))
            throw new ArgumentException("This actor is already added to the actor manager.", nameof(actor));
        if (actor.ActorManager != null)
            throw new ArgumentException("This actor is already used by another actor manager.", nameof(actor));

        actor.ActorManager = this;

        foreach (var component in actor.Components)
        {
            AddComponent(component, actor);
        }

        foreach (var child in actor.Children)
        {
            AddActorInternal(child);
        }

        actor.Children.CollectionChanged += OnChildrenCollectionChanged;
        actor.Components.CollectionChanged += OnComponentsCollectionChanged;
    }

    protected void RemoveRoot(Actor actor)
    {
        if (actor.Parent != null)
        {
            throw new ArgumentException("This actor should not have a parent.", nameof(actor));
        }

        RemoveActorInternal(actor);
    }
    private void RemoveActorInternal(Actor actor)
    {
        if (!_actors.Remove(actor.Id))
            throw new ArgumentException("This actor is not part of the actor manager.", nameof(actor));
        if (actor.ActorManager != this)
            throw new ArgumentException("This actor is not part of this actor manager.", nameof(actor));

        actor.ActorManager = null;

        actor.Components.CollectionChanged -= OnComponentsCollectionChanged;
        actor.Children.CollectionChanged -= OnChildrenCollectionChanged;

        foreach (var component in actor.Components)
        {
            RemoveComponent(component, actor);
        }

        foreach (var child in actor.Children)
        {
            RemoveActorInternal(child);
        }
    }

    protected virtual void AddComponent(ActorComponent component, Actor actor)
        => CheckActorComponentWithSystems(component, actor, false);
    protected virtual void RemoveComponent(ActorComponent component, Actor actor)
        => CheckActorComponentWithSystems(component, actor, true);

    private void CheckActorComponentWithSystems(ActorComponent component, Actor actor, bool bRemove)
    {
        var componentType = component.GetType();
        if (!_systemsPerComponentType.TryGetValue(componentType, out var systemsForComponent))
        {
            if (!bRemove)
            {
                CollectNewActorSystems(componentType);
            }

            systemsForComponent = [];
            foreach (var system in _systemsToLoad) AddIfAccepted(system);
            foreach (var system in Systems.Values) AddIfAccepted(system);
            _systemsPerComponentType.Add(componentType, systemsForComponent);
        }

        foreach (var system in systemsForComponent)
        {
            system.ProcessActorComponent(component, actor);
        }

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

    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var actor in e.NewItems!.Cast<Actor>())
                {
                    AddActorInternal(actor);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var actor in e.OldItems!.Cast<Actor>())
                {
                    RemoveActorInternal(actor);
                }
                break;
        }
    }

    private void OnComponentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not ActorComponentCollection componentCollection) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var component in e.NewItems!.Cast<ActorComponent>())
                {
                    AddComponent(component, componentCollection.Actor);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var component in e.OldItems!.Cast<ActorComponent>())
                {
                    RemoveComponent(component, componentCollection.Actor);
                }
                break;
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
            system.Load();

            Systems.Add(system.Order, system);
            count++;
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
        ImGui.SetWindowFontScale(0.85f);
        ImGui.TextDisabled($"API: {Renderer.Name} | GPU: {Renderer.DeviceInfo.Name}");
        ImGui.SetWindowFontScale(1.0f);

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

    public virtual void Dispose()
    {
        foreach (var system in Systems.Values)
            system.Dispose();

        Systems.Clear();
        ThreadManager.Dispose();
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
