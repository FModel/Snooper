struct PerMeshData
{
    vec3 Center;
    float SphereRadius;
    vec3 Extents;
    uint MaxLOD;
    vec2 DrawDistances; // min and max draw distances
    int OverrideLod; // -1 for automatic LOD selection, >= 0 to force a specific LOD
    uint ColorMode; // FragmentColorMode for this mesh, 0 to follow the global uniform
};

layout(std430, binding = BINDING_MESH_DATA) readonly buffer PerMeshDataBuffer
{
    PerMeshData uMeshDataBuffer[];
};
