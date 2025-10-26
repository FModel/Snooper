using System.Numerics;

namespace Snooper.Rendering.Components.Descriptors;

public unsafe struct PrimitiveOffsets
{
    public readonly Vector3 Center;
    public readonly float SphereRadius;
    public readonly Vector3 Extents;
    public uint MaxLOD = 0;
    public int OverrideLod = -1; // -1 for automatic LOD selection, >= 0 to force a specific LOD
    public Vector3 Padding;
    
    // vec4 alignment needed
    public fixed uint LOD_FirstIndex[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseVertex[Settings.MaxNumberOfLods];
    public fixed float LOD_ScreenSize[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionCount[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionOffset[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseColor[Settings.MaxNumberOfLods];

    public PrimitiveOffsets(CullingBounds bounds)
    {
        Center = bounds.Center;
        SphereRadius = bounds.Extents.Length();
        Extents = bounds.Extents;
        
        for (var i = 0; i < Settings.MaxNumberOfLods; i++)
        {
            LOD_BaseColor[i] = uint.MaxValue;
        }
    }
}

public struct SectionOffsets(SectionDescriptor descriptor)
{
    public readonly uint FirstIndex = descriptor.FirstIndex;
    public readonly uint IndexCount = descriptor.IndexCount;
    public readonly uint MaterialIndex = descriptor.MaterialIndex;
}