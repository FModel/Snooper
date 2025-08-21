using Snooper.Core.Containers.Resources;

namespace Snooper.Rendering.Components.Primitive;

public class MaterialSection(uint materialIndex)
{
    private static int _nextId = 0;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);
    
    public readonly uint MaterialIndex = materialIndex;

    public IndirectDrawMetadata DrawMetadata = new();
    public IDrawDataContainer? DrawDataContainer = null;
    public bool IsTranslucent => DrawDataContainer?.IsTranslucent ?? false;
    
    public bool IsGenerated => DrawMetadata.BaseInstance >= 0;

    public override bool Equals(object? obj) => obj is MaterialSection section && section.SectionId.Equals(SectionId);
    public override int GetHashCode() => SectionId.GetHashCode();
}