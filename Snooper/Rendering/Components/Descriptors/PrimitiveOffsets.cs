namespace Snooper.Rendering.Components.Descriptors;

public unsafe struct PrimitiveOffsets(CullingBounds bounds)
{
    public CullingBounds Bounds = bounds;
    
    public fixed uint LOD_FirstIndex[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseVertex[Settings.MaxNumberOfLods];
    public fixed float LOD_ScreenSize[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionCount[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionOffset[Settings.MaxNumberOfLods];
}