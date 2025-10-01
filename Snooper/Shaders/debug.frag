layout (location = 1) out uint gPicking;

struct PerDrawData
{
    bool IsReady;
    vec3 Color;
};

layout(std430, binding = 2) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

#include "Buffers/common.frag"

out vec4 FragColor;

void main()
{
    PerDrawData drawData = uDrawDataBuffer[gDrawID];
    
    vec3 color = vec3(0.75);
    if (drawData.IsReady)
    {
        color = drawData.Color;
    }
    
    FragColor = vec4(color, 1.0);

    gPicking = 0u;
}