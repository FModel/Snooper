#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

out uvec4 FragColor;

void main()
{
    FragColor = uvec4(uDrawCommandBuffer[gDrawID].PickingId, 0, 0, 1);
}