using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Cache;

namespace Snooper.Rendering.Components.Descriptors;

public class MaterialSection(uint index)
{
    private static int _nextId;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);

    public readonly uint Index = index;

    public BufferAllocation? Allocation { get; internal set; } // set when added to the material data buffer

    internal string? CacheKey
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            Override = null;
            if (field != null) _onMaterialDataContainerSet?.Invoke(this);
        }
    }

    internal IMaterialDataContainer? InlineContainer
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            if (field != null) _onMaterialDataContainerSet?.Invoke(this);
        }
    }

    private string? _originalCacheKey;
    private bool _hasOriginalCacheKey;

    public bool IsSwapped => _hasOriginalCacheKey && _originalCacheKey != CacheKey;
    public bool IsEdited => Override != null || IsSwapped;

    public MaterialDataContainer? Override { get; private set; }
    public IMaterialDataContainer? MaterialDataContainer => Override ?? (CacheKey != null ? MaterialCache.Resolve(CacheKey) : InlineContainer);

    public void SwapMaterial(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey) || cacheKey == CacheKey) return;

        if (!_hasOriginalCacheKey)
        {
            _originalCacheKey = CacheKey;
            _hasOriginalCacheKey = true;
        }

        CacheKey = cacheKey;
    }

    public MaterialDataContainer? BeginEdit()
    {
        if (Override != null) return Override;
        if (MaterialDataContainer is not MaterialDataContainer source) return null;
        if (!source.IsGpuDataReady) return null;

        return Override = source.Clone();
    }

    public void CommitEdit()
    {
        if (Override is not { } edited) return;

        edited.FinalizeGpuData();
        ContainerReady();
    }

    public void RevertEdit()
    {
        if (!IsEdited) return;

        Override = null;

        if (IsSwapped)
        {
            CacheKey = _originalCacheKey;
            return; // TextureCache will call ContainerReady anyway
        }

        ContainerReady();
    }

    private Action<MaterialSection>? _onMaterialDataContainerSet;
    public event Action<MaterialSection>? OnMaterialDataContainerSet
    {
        add
        {
            _onMaterialDataContainerSet += value;
            // fire immediately if the material data container is already set
            if (MaterialDataContainer != null && value != null)
                value(this);
        }
        remove => _onMaterialDataContainerSet -= value;
    }

    public bool IsTranslucent => MaterialDataContainer?.IsTranslucent ?? false;

    public event Action<MaterialSection>? OnContainerReady;
    internal void ContainerReady() => OnContainerReady?.Invoke(this);

    public override bool Equals(object? obj) => obj is MaterialSection s && s.SectionId.Equals(SectionId);
    public override int GetHashCode() => SectionId.GetHashCode();
}
