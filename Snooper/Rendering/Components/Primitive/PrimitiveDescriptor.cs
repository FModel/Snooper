namespace Snooper.Rendering.Components.Primitive;

public unsafe struct PrimitiveDescriptor(CullingBounds bounds)
{
    public readonly CullingBounds Bounds = bounds;
    
    public fixed uint LOD_IndexCount[8];
    public fixed uint LOD_FirstIndex[8];
    public fixed uint LOD_BaseVertex[8];
}

public readonly struct PrimitiveLodDescriptor(uint firstIndex, uint baseVertex)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint BaseVertex = baseVertex;
}

public readonly struct PrimitiveSectionDescriptor(uint firstIndex, uint indexCount, uint materialIndex)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint IndexCount = indexCount;
    public readonly uint MaterialIndex = materialIndex;
}