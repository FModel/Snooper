using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Systems;

public class TextureManager : IGameSystem, IMemoryDetailsProvider
{
    private bool _isLoaded;
    private int _totalTexturesRequested;

    public int LoadedTextureCount => _textures.Count;
    public int PendingTextureCount => _loadQueue.Count;
    public int BindlessTextureCount => _bindless.Count;
    public bool IsLoading => _isLoaded && PendingTextureCount > 0;

    public float LoadingProgress
    {
        get
        {
            if (!IsLoading) return 1f;
            if (_totalTexturesRequested == 0) return 1f;
            return (float)LoadedTextureCount / _totalTexturesRequested;
        }
    }

    /// <summary>
    /// fired when a material section has all its textures loaded and ready.
    /// </summary>
    public event Action<MaterialSection>? OnMaterialReady;

    private readonly Dictionary<FGuid, Texture> _textures = [];
    private readonly Dictionary<FGuid, BindlessTexture> _bindless = [];
    private readonly Queue<Texture> _loadQueue = [];
    private readonly HashSet<FGuid> _queuedGuids = [];

    // tracks which material sections are waiting for which textures
    private readonly Dictionary<FGuid, List<MaterialDependency>> _dependencies = [];
    private readonly Dictionary<int, MaterialLoadState> _states = [];

    public void AddRange(MaterialSection[] materials)
    {
        foreach (var material in materials)
        {
            if (material.MaterialDataContainer is null) continue;

            if (!material.MaterialDataContainer.HasTextures)
            {
                OnMaterialReady?.Invoke(material);
                continue;
            }

            var textures = material.MaterialDataContainer.GetTextures();
            _states[material.SectionId] = new MaterialLoadState(material, textures.Count);

            foreach (var (key, texture) in textures)
            {
                QueueTexture(texture, material.SectionId, key);
            }
        }
    }

    private void QueueTexture(Texture texture, int sectionId, string key)
    {
        var guid = texture.Guid;
        var dependency = new MaterialDependency(sectionId, key);

        if (_bindless.ContainsKey(guid))
        {
            ApplyBindlessToMaterial(guid, dependency);
            return;
        }

        if (!_dependencies.TryGetValue(guid, out var dependencies))
        {
            dependencies = [];
            _dependencies[guid] = dependencies;
        }

        if (!dependencies.Exists(d => d.SectionId == sectionId && d.Key == key))
        {
            dependencies.Add(dependency);
        }

        if (_textures.ContainsKey(guid) || _queuedGuids.Contains(guid))
        {
            return;
        }

        _loadQueue.Enqueue(texture);
        _queuedGuids.Add(guid);
        _totalTexturesRequested++;
    }

    public void Load() => _isLoaded = true;
    public void Update(float delta) => ProcessTextureQueue(1);
    public void Render(CameraComponent camera) => throw new NotImplementedException();

    private void ProcessTextureQueue(int limit)
    {
        var processed = 0;
        while (_loadQueue.Count > 0 && (limit == 0 || processed < limit))
        {
            var texture = _loadQueue.Dequeue();
            var guid = texture.Guid;
            _queuedGuids.Remove(guid);

            texture.TextureReadyForBindless += () => OnTextureReady(guid, texture);
            texture.Generate();

            _textures.Add(guid, texture);
            processed++;
        }
    }

    private void OnTextureReady(FGuid guid, Texture texture)
    {
        Log.Debug("Texture {Guid} is ready for bindless usage.", guid);

        var bindless = new BindlessTexture(texture);
        _bindless.Add(guid, bindless);

        if (_dependencies.TryGetValue(guid, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                ApplyBindlessToMaterial(guid, dependency);
            }

            _dependencies.Remove(guid);
        }
    }

    private void ApplyBindlessToMaterial(FGuid guid, MaterialDependency dependency)
    {
        if (!_bindless.TryGetValue(guid, out var bindless))
        {
            Log.Warning("Attempted to apply non-existent bindless texture {Guid}", guid);
            return;
        }

        if (!_states.TryGetValue(dependency.SectionId, out var state))
        {
            return;
        }

        state.Section.MaterialDataContainer?.SetBindlessTexture(dependency.Key, bindless);
        state.RemainingTextures--;

        if (state.RemainingTextures <= 0)
        {
            _states.Remove(dependency.SectionId);
            OnMaterialReady?.Invoke(state.Section);
        }
    }

    public void Dispose()
    {
        foreach (var texture in _bindless.Values)
        {
            texture.Dispose();
        }

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
        _bindless.Clear();
        _loadQueue.Clear();
        _queuedGuids.Clear();
        _dependencies.Clear();
        _states.Clear();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            foreach (var texture in _textures.Values)
            {
                total += texture.Allocated;
            }
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            foreach (var texture in _textures.Values)
            {
                total += texture.Used;
            }
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var texture in _textures.Values)
        {
            yield return new MemoryDetail(texture.Name, texture);
        }
    }

    private record MaterialDependency(int SectionId, string Key);

    private class MaterialLoadState(MaterialSection section, int textureCount)
    {
        public MaterialSection Section { get; } = section;
        public int RemainingTextures { get; set; } = textureCount;
    }
}
