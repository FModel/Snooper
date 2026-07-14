using System.Numerics;

namespace Snooper.Rendering.Components.Descriptors;

public struct PerMeshData(CullingBounds bounds, uint maxLod, Vector2 drawDistances)
{
    public readonly Vector3 Center = bounds.Center;
    public readonly float SphereRadius = bounds.Extents.Length();
    public readonly Vector3 Extents = bounds.Extents;
    public readonly uint MaxLOD = maxLod;
    public readonly Vector2 DrawDistances = drawDistances;
    public int OverrideLod = -1; // -1 for automatic LOD selection, >= 0 to force a specific LOD
    public readonly uint Padding = 0;

    public const int OverrideLodOffset = 40; // byte offset of OverrideLod for partial updates
}

public unsafe struct PrimitiveOffsets
{
    // vec4 alignment needed
    public fixed uint LOD_FirstIndex[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseVertex[Settings.MaxNumberOfLods];
    public fixed float LOD_ScreenSize[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionCount[Settings.MaxNumberOfLods];
    public fixed uint LOD_SectionOffset[Settings.MaxNumberOfLods];
    public fixed uint LOD_BaseColor[Settings.MaxNumberOfLods];

    public PrimitiveOffsets()
    {
        for (var i = 0; i < Settings.MaxNumberOfLods; i++)
        {
            LOD_BaseColor[i] = uint.MaxValue;
        }
    }
}

public readonly struct SectionOffsets(SectionDescriptor descriptor)
{
    public readonly uint FirstIndex = descriptor.FirstIndex;
    public readonly uint IndexCount = descriptor.IndexCount;
    public readonly uint MaterialIndex = descriptor.MaterialIndex;
}
