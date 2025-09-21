struct DrawElementsIndirectCommand // 1 draw command per section per model in LOD 0
{
    uint IndexCount;
    uint InstanceCount;
    uint FirstIndex;
    uint BaseVertex;
    uint BaseInstance;
    
    uint PickingId;
    uint OriginalInstanceCount;
    uint OriginalBaseInstance;
    uint ModelId; // model offset in PrimitiveDescriptorsBuffer
    uint SectionId; // section index in the current model (0-X)
};

layout(std430, binding = 0) buffer PerDrawCommandBuffer
{
    DrawElementsIndirectCommand uDrawCommandBuffer[];
};