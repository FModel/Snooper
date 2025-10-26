using Snooper.Core.Containers.Resources;

namespace Snooper.Rendering.Components.Descriptors;

public class MaterialSection(uint materialIndex) : IDisposable
{
    private static int _nextId = 0;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);
    
    public readonly uint MaterialIndex = materialIndex;

    public string Name { get; internal set; } = "Unnamed";
    public uint MaterialOffset { get; internal set; } = 0; // set when added to the material data buffer
    public IMaterialDataContainer? MaterialDataContainer { get; internal set; } = null; // set when the material is loaded

    public bool IsTranslucent => MaterialDataContainer?.IsTranslucent ?? false;

    public override bool Equals(object? obj) => obj is MaterialSection section && section.SectionId.Equals(SectionId);
    public override int GetHashCode() => SectionId.GetHashCode();
    
    public void Dispose()
    {
        MaterialDataContainer?.Dispose();
    }
}