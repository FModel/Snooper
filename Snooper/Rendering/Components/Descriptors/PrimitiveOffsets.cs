using System.Numerics;

namespace Snooper.Rendering.Components.Descriptors;

public unsafe struct PrimitiveOffsets(CullingBounds bounds)
{
    public CullingBounds Bounds = bounds;
    
    public int OverrideLod = -1; // -1 for automatic LOD selection, >= 0 to force a specific LOD
    public Vector3 Padding;
    
    // vec4 alignment needed
    public fixed uint LOD_FirstIndex[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseVertex[Settings.MaxNumberOfLods];
    public fixed float LOD_ScreenSize[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionCount[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionOffset[Settings.MaxNumberOfLods];
}