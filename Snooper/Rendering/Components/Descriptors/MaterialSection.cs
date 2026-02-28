using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Cache;

namespace Snooper.Rendering.Components.Descriptors;

public class MaterialSection : IDisposable
{
    private static int _nextId;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);

    public BufferAllocation? Allocation { get; internal set; } // set when added to the material data buffer

    internal string? CacheKey
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
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

    public IMaterialDataContainer? MaterialDataContainer => CacheKey != null ? MaterialCache.Resolve(CacheKey) : InlineContainer;

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

    public void Dispose()
    {
        InlineContainer?.Dispose();
    }
}
