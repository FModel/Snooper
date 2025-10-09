namespace Snooper.Rendering.Components.Descriptors;

public struct SectionDescriptor(uint firstIndex, uint indexCount, uint materialIndex)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint IndexCount = indexCount;
    public readonly uint MaterialIndex = materialIndex;
}