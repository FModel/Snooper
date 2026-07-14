struct PerLightData
{
    vec3 position;
    float range;
    vec3 color;
    uint type; // 0 = point, 1 = spot, 2 = rect
    vec3 direction;
    float spotAngle;
    float spotOuterAngle;
    float intensity;
    float sizeX;           // Rect light width
    float sizeY;           // Rect light height
    vec3 upVector;
    uint UseInverseSquaredFalloff;
};

layout(std430, binding = BINDING_LIGHT_DATA) readonly buffer LightBuffer
{
    PerLightData lights[];
};

struct ClusterData
{
    uint offset;
    uint count;
};

layout(std430, binding = BINDING_LIGHT_CLUSTER_DATA) buffer ClusterDataBuffer
{
    ClusterData clusterData[];
};

layout(std430, binding = BINDING_LIGHT_INDEX_LIST) buffer LightIndexList
{
    uint lightIndices[];
};
