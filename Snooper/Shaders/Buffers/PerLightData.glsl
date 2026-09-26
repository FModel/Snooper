#define LIGHT_TYPE_MASK 3u
#define LIGHT_INVERSE_SQUARED_FALLOFF 4u

struct PerLightData
{
    vec3 position;
    float range;
    vec3 color;
    uint flags;
    vec3 direction;
    float spotAngle;
    float spotOuterAngle;
    float intensity;
    float sizeX;           // Rect light width
    float sizeY;           // Rect light height
    vec3 tangent;
    float maxDrawDistance;
    float maxDistanceFadeRange;
    float falloffExponent;
    float sourceRadius;
    float sourceLength;
};

float LightDistanceFade(PerLightData light, float cameraDistance)
{
    if (light.maxDrawDistance <= 0.0) return 1.0;
    return 1.0 - smoothstep(light.maxDrawDistance - light.maxDistanceFadeRange, light.maxDrawDistance, cameraDistance);
}

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
