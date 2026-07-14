struct PerDrawData // extra per-draw data, index-aligned with the draw command buffer (index with gl_DrawID)
{
    uint MeshIndex; // index into the per-mesh buffers (PerMeshData, PrimitiveDescriptors)
    uint SectionId; // section index in the current model (0-X)
    uint BaseMaterial; // offset of the first material this component uses in the material buffer
    uint MaterialIndex; // index of the material relative to BaseMaterial (written by culling per LOD)
    uint PickingId;
    uint OriginalInstanceCount;
    uint OriginalBaseInstance;
    uint CastShadow; // 0 or 1
    uint Lod; // LOD chosen by the culling pass
    uint BaseColor; // offset into the vertex color buffer (written by culling per LOD)
};

layout(std430, binding = BINDING_DRAW_DATA) buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};
