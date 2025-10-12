layout (location = 1) out uint gPicking;

struct PerDrawData
{
    bool IsReady;
    vec3 LineColor;
};

layout(std430, binding = 2) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

out vec4 FragColor;

void main()
{
    PerDrawData drawData = uDrawDataBuffer[gDrawID];
    
    vec3 color = vec3(0.75);
    if (drawData.IsReady)
    {
        color = drawData.LineColor;
    }
    
    FragColor = vec4(color, 1.0);

    gPicking = uDrawCommandBuffer[gDrawID].PickingId;
}