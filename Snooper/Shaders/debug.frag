layout (location = 1) out uint gPicking;

struct PerMaterialData
{
    bool IsReady;
    float LineThickness;
    vec3 LineColor;
};

layout(std430, binding = 2) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

#include "Buffers/PerDrawCommand.glsl"

in flat uint gDrawID;

out vec4 FragColor;

void main()
{
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterialOffset + cmd.MaterialIndex];
    
    vec3 color = vec3(0.75);
    if (materialData.IsReady)
    {
        color = materialData.LineColor;
    }
    
    FragColor = vec4(color, 1.0);

    gPicking = cmd.PickingId;
}