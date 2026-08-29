layout (location = 1) out uint gPicking;

struct PerMaterialData
{
    bool IsReady;
    float LineThickness;
    vec3 LineColor;
};

layout(std430, binding = BINDING_MATERIAL_DATA) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

#include "Buffers/PerDrawData.glsl"

in flat uint gDrawID;

out vec4 FragColor;

void main()
{
    PerDrawStatic draw = uDrawStatic[gDrawID];
    PerDrawCulled culled = FetchCulled(gDrawID);
    PerMaterialData materialData = uMaterialDataBuffer[draw.BaseMaterial + culled.MaterialIndex];

    vec3 color = vec3(0.75);
    if (materialData.IsReady)
    {
        color = materialData.LineColor;
    }

    FragColor = vec4(color, 1.0);

    gPicking = draw.PickingId;
}
