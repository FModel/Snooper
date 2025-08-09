namespace Snooper.Rendering.Components.Primitive;

public unsafe struct PrimitiveDescriptor(CullingBounds bounds)
{
    public CullingBounds Bounds = bounds;
    
    public fixed uint LOD_FirstIndex[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseVertex[Settings.MaxNumberOfLods];
    public fixed uint LOD_ScreenSize[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionCount[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionOffset[Settings.MaxNumberOfLods];
}

public struct PrimitiveSectionDescriptor(uint firstIndex, uint indexCount, uint materialIndex)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint IndexCount = indexCount;
    public readonly uint MaterialIndex = materialIndex;
}