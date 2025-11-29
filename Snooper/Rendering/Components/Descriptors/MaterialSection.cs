using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;

namespace Snooper.Rendering.Components.Descriptors;

public class MaterialSection : IDisposable
{
    private static int _nextId;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);

    public BufferAllocation? Allocation { get; internal set; } // set when added to the material data buffer

    private IMaterialDataContainer? _materialDataContainer;
    public IMaterialDataContainer? MaterialDataContainer
    {
        get => _materialDataContainer;
        internal set
        {
            if (_materialDataContainer == value) return;

            _materialDataContainer = value;
            _onMaterialDataContainerSet?.Invoke(this);
        }
    }

    private Action<MaterialSection>? _onMaterialDataContainerSet;
    public event Action<MaterialSection>? OnMaterialDataContainerSet
    {
        add
        {
            _onMaterialDataContainerSet += value;
            // fire immediately if the material data container is already set
            if (_materialDataContainer != null && value != null)
            {
                value(this);
            }
        }
        remove => _onMaterialDataContainerSet -= value;
    }

    public bool IsTranslucent => MaterialDataContainer?.IsTranslucent ?? false;

    public override bool Equals(object? obj) => obj is MaterialSection section && section.SectionId.Equals(SectionId);
    public override int GetHashCode() => SectionId.GetHashCode();

    public void Dispose()
    {
        MaterialDataContainer?.Dispose();
    }
}
