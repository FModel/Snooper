using Snooper.Core.Containers.Resources;

namespace Snooper.Rendering.Components.Primitive;

public class PrimitiveSection(int firstIndex, int indexCount)
{
    private static int _nextId = 0;
    public readonly int SectionId = Interlocked.Increment(ref _nextId);
    
    public readonly int FirstIndex = firstIndex;
    public readonly int IndexCount = indexCount;

    public IndirectDrawMetadata DrawMetadata = new();
    public IDrawDataContainer? DrawDataContainer;
    
    public bool IsGenerated => DrawMetadata.BaseInstance >= 0;

    public override bool Equals(object? obj) => obj is PrimitiveSection section && section.SectionId.Equals(SectionId);
    public override int GetHashCode() => SectionId.GetHashCode();
}