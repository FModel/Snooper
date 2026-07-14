struct DrawElementsIndirectCommand // 1 draw command per section per model in LOD 0, exact glMultiDrawElementsIndirect layout
{
    uint IndexCount;
    uint InstanceCount;
    uint FirstIndex;
    uint BaseVertex;
    uint BaseInstance;
};

layout(std430, binding = BINDING_DRAW_COMMANDS) buffer PerDrawCommandBuffer
{
    DrawElementsIndirectCommand uDrawCommandBuffer[];
};
