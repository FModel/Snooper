#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

struct PerDrawData
{
    bool IsReady;
    float OpacityMask;
    sampler2D Sprite;
};

layout(std430, binding = 2) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

in vec2 vTexCoords;

out vec4 FragColor;

void main()
{
    PerDrawData drawData = uDrawDataBuffer[gDrawID];
    
    vec4 color = vec4(1.0);
    if (drawData.IsReady)
    {
        color = texture(drawData.Sprite, vTexCoords);
        if (color.a < drawData.OpacityMask)
        {
            discard;
        }
    }

    FragColor = pow(color, vec4(1.0 / 2.2));
    
    gPicking = uDrawCommandBuffer[gDrawID].PickingId;
}