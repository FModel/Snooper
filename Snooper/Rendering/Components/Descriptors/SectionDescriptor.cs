namespace Snooper.Rendering.Components.Descriptors;

public struct SectionDescriptor(uint firstIndex, uint indexCount, uint materialIndex, string? name = null)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint IndexCount = indexCount;
    public readonly uint MaterialIndex = materialIndex;
    public readonly string Name = name ?? Settings.NoName;
}
