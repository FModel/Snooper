struct PerDrawStatic
{
    uint MeshIndex; // index into the per-mesh buffers (PerMeshData, PrimitiveDescriptors)
    uint SectionId; // section index in the current model (0-X)
    uint BaseMaterial; // the component's first entry in the material table
    uint PickingId;
    uint OriginalInstanceCount;
    uint OriginalBaseInstance;
    uint CastShadow; // 0 or 1
    float MinDrawDistance;
    float MaxDrawDistance; // 0 for no limit
    uint Outlined; // 0 or 1
};

layout(std430, binding = BINDING_DRAW_STATIC) readonly buffer PerDrawStaticBuffer
{
    PerDrawStatic uDrawStatic[];
};

struct PerDrawCulled
{
    uint Lod;
    uint MaterialIndex; // the material this draw uses relative to BaseMaterial in the material table
    uint BaseColor; // offset into the vertex color buffer
};

layout(std430, binding = BINDING_DRAW_CULLED) buffer PerDrawCulledBuffer
{
    PerDrawCulled uDrawCulled[];
};

layout(std430, binding = BINDING_MATERIAL_TABLE) readonly buffer MaterialTableBuffer
{
    uint uMaterialTable[];
};

uniform uint uViewBase;

PerDrawCulled FetchCulled(uint drawId)
{
    return uDrawCulled[uViewBase + drawId];
}

uint MaterialSlot(PerDrawStatic draw, PerDrawCulled culled)
{
    return uMaterialTable[draw.BaseMaterial + culled.MaterialIndex];
}
