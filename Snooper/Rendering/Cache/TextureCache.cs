using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Descriptors;
using System.Collections.Concurrent;

namespace Snooper.Rendering.Cache;

public static class TextureCache
{
    private static int _totalTexturesRequested;

    public static int LoadedTextureCount => _textures.Count;
    public static int PendingTextureCount => _loadQueue.Count;
    public static int BindlessTextureCount => _bindless.Count;
    public static bool IsLoading => PendingTextureCount > 0;

    public static float LoadingProgress
    {
        get
        {
            if (!IsLoading) return 1f;
            if (_totalTexturesRequested == 0) return 1f;
            return (float)LoadedTextureCount / _totalTexturesRequested;
        }
    }

    private static readonly ConcurrentDictionary<FGuid, Texture> _textures = new();
    private static readonly ConcurrentDictionary<FGuid, BindlessTexture> _bindless = new();
    private static readonly ConcurrentQueue<Texture> _loadQueue = new();
    private static readonly ConcurrentDictionary<FGuid, byte> _knownGuids = new();

    private static readonly ConcurrentDictionary<FGuid, ConcurrentBag<ContainerDependency>> _dependencies = new();
    private static readonly ConcurrentDictionary<string, ContainerLoadState> _states = new();
    private static readonly ConcurrentDictionary<string, byte> _registeredKeys = new();

    public static void Add(MaterialSection section)
    {
        var cacheKey = section.CacheKey;

        if (string.IsNullOrEmpty(cacheKey))
        {
            var inline = section.InlineContainer;
            if (inline is null) return;

            if (!inline.HasTextures)
            {
                inline.FinalizeGpuData();
                section.ContainerReady();
                return;
            }

            var inlineKey = $"__inline_{section.SectionId}";
            var inlineTextures = inline.GetTextures();
            _states[inlineKey] = new ContainerLoadState(inline, inlineTextures.Count);
            _states[inlineKey].Sections.Add(section);
            foreach (var (key, texture) in inlineTextures)
                QueueTexture(texture, inlineKey, key);
            return;
        }

        var container = MaterialCache.Resolve(cacheKey);
        if (container is null) return;

        if (!container.HasTextures)
        {
            container.FinalizeGpuData();
            section.ContainerReady();
            return;
        }

        var textures = container.GetTextures();

        if (_registeredKeys.ContainsKey(cacheKey))
        {
            if (_states.TryGetValue(cacheKey, out var existing))
            {
                // still loading — register this section so it gets notified when done
                existing.Sections.Add(section);
            }
            else
            {
                // already fully loaded — fire immediately
                section.ContainerReady();
            }
            return;
        }

        _registeredKeys.TryAdd(cacheKey, 0);
        var state = new ContainerLoadState(container, textures.Count);
        state.Sections.Add(section);
        _states[cacheKey] = state;

        foreach (var (key, texture) in textures)
            QueueTexture(texture, cacheKey, key);
    }

    private static void QueueTexture(Texture texture, string containerKey, string textureKey)
    {
        var guid = texture.Guid;
        var dependency = new ContainerDependency(containerKey, textureKey);

        if (_bindless.ContainsKey(guid))
        {
            ApplyBindlessToContainer(guid, dependency);
            return;
        }

        _dependencies.GetOrAdd(guid, _ => []).Add(dependency);

        if (_knownGuids.TryAdd(guid, 0))
        {
            _loadQueue.Enqueue(texture);
            Interlocked.Increment(ref _totalTexturesRequested);
        }
    }

    public static void Update() => ProcessTextureQueue(1);

    private static void ProcessTextureQueue(int limit)
    {
        var processed = 0;
        while (processed < limit && _loadQueue.TryDequeue(out var texture))
        {
            texture.TextureReadyForBindless += () => OnTextureReady(texture.Guid, texture);
            texture.Generate();
            processed++;
        }
    }

    private static void OnTextureReady(FGuid guid, Texture texture)
    {
        Log.Debug("Texture {Guid} is ready for bindless usage.", guid);

        _textures.TryAdd(guid, texture);

        var bindless = new BindlessTexture(texture);
        bindless.Generate();
        bindless.MakeResident();
        _bindless.TryAdd(guid, bindless);

        if (_dependencies.TryRemove(guid, out var dependencies))
        {
            foreach (var dependency in dependencies)
                ApplyBindlessToContainer(guid, dependency);
        }
    }

    private static void ApplyBindlessToContainer(FGuid guid, ContainerDependency dependency)
    {
        if (!_bindless.TryGetValue(guid, out var bindless))
        {
            Log.Warning("Attempted to apply non-existent bindless texture {Guid}", guid);
            return;
        }

        if (!_states.TryGetValue(dependency.ContainerKey, out var state))
            return;

        state.Container.SetBindlessTexture(dependency.TextureKey, bindless);

        var remaining = Interlocked.Decrement(ref state.RemainingTextures);
        if (remaining > 0) return;

        _states.TryRemove(dependency.ContainerKey, out _);
        state.Container.FinalizeGpuData();

        foreach (var section in state.Sections)
            section.ContainerReady();
    }

    public static void ClearAndDispose()
    {
        foreach (var texture in _bindless.Values)
            texture.Dispose();
        foreach (var texture in _textures.Values)
            texture.Dispose();

        Log.Information("Clearing texture cache with {Count} entries", _textures.Count);
        _textures.Clear();
        _bindless.Clear();
        _loadQueue.Clear();
        _knownGuids.Clear();
        _dependencies.Clear();
        _states.Clear();
        _registeredKeys.Clear();
        _totalTexturesRequested = 0;
    }

    public static long Allocated
    {
        get
        {
            long total = 0;
            foreach (var texture in _textures.Values)
                total += texture.Allocated;
            return total;
        }
    }

    public static long Used
    {
        get
        {
            long total = 0;
            foreach (var texture in _textures.Values)
                total += texture.Used;
            return total;
        }
    }

    public static IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var texture in _textures.Values)
            yield return new MemoryDetail(texture.Name, texture);
    }

    private record ContainerDependency(string ContainerKey, string TextureKey);

    private class ContainerLoadState(IMaterialDataContainer container, int textureCount)
    {
        public IMaterialDataContainer Container { get; } = container;
        public int RemainingTextures = textureCount;
        public List<MaterialSection> Sections { get; } = [];
    }
}
