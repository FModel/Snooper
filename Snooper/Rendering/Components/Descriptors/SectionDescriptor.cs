namespace Snooper.Rendering.Components.Descriptors;

public readonly struct SectionDescriptor(uint firstIndex, uint indexCount, uint materialIndex, bool castShadow = false, string? name = null)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint IndexCount = indexCount;
    public readonly uint MaterialIndex = materialIndex;
    public readonly bool CastShadow = castShadow;
    public readonly string Name = name ?? Settings.NoName;
}
