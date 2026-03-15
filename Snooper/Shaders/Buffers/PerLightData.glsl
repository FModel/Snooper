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

layout(std430, binding = 0) readonly buffer LightBuffer
{
    PerLightData lights[];
};

struct ClusterData
{
    uint offset;
    uint count;
};

layout(std430, binding = 1) buffer ClusterDataBuffer
{
    ClusterData clusterData[];
};

layout(std430, binding = 2) buffer LightIndexList
{
    uint lightIndices[];
};
