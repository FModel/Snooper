struct DrawElementsIndirectCommand // 1 draw command per section per model in LOD 0
{
    uint IndexCount;
    uint InstanceCount;
    uint FirstIndex;
    uint BaseVertex;
    uint BaseInstance;

    uint BaseGeometry; // offset of this geometry in the culling buffer
    uint BaseColor;
    uint BaseMaterial; // offset of the first material this geometry uses in the material buffer
    uint MaterialIndex; // index of the material relative to BaseMaterial
    uint PickingId;
    uint OriginalInstanceCount;
    uint OriginalBaseInstance;
    uint SectionId; // section index in the current model (0-X)
    uint CastShadow; // 0 or 1
};

layout(std430, binding = 0) buffer PerDrawCommandBuffer
{
    DrawElementsIndirectCommand uDrawCommandBuffer[];
};
