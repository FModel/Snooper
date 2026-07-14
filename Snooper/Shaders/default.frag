layout (location = 1) out uint gPicking;

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

out vec4 FragColor;

void main()
{
    FragColor = vec4(0.0, 0.0, 1.0, 0.75);

    gPicking = uDrawDataBuffer[gDrawID].PickingId;
}