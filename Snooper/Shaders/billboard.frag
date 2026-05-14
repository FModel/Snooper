#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

struct PerMaterialData
{
    bool IsReady;
    float OpacityMask;
    sampler2D Sprite;
};

layout(std430, binding = 2) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

in vec2 vTexCoords;

out vec4 FragColor;

void main()
{
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];

    vec4 color = vec4(1.0);
    if (materialData.IsReady)
    {
        color = texture(materialData.Sprite, vTexCoords);
        if (color.a < materialData.OpacityMask)
        {
            discard;
        }
    }

    FragColor = pow(color, vec4(1.0 / 2.2));

    gPicking = cmd.PickingId;
}
