struct PerDrawStatic
{
    uint MeshIndex; // index into the per-mesh buffers (PerMeshData, PrimitiveDescriptors)
    uint SectionId; // section index in the current model (0-X)
    uint BaseMaterial; // offset of the first material this component uses in the material buffer
    uint PickingId;
    uint OriginalInstanceCount;
    uint OriginalBaseInstance;
    uint CastShadow; // 0 or 1
    float MinDrawDistance;
    float MaxDrawDistance; // 0 for no limit
};

layout(std430, binding = BINDING_DRAW_STATIC) readonly buffer PerDrawStaticBuffer
{
    PerDrawStatic uDrawStatic[];
};

struct PerDrawCulled
{
    uint Lod;
    uint MaterialIndex; // index of the material this draw uses relative to BaseMaterial
    uint BaseColor; // offset into the vertex color buffer
};

layout(std430, binding = BINDING_DRAW_CULLED) buffer PerDrawCulledBuffer
{
    PerDrawCulled uDrawCulled[];
};

uniform uint uViewBase;

PerDrawCulled FetchCulled(uint drawId)
{
    return uDrawCulled[uViewBase + drawId];
}
