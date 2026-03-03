layout (location = 0) in uvec2 aPosHalf;

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"

void main()
{
    vec2 posXY = unpackHalf2x16(aPosHalf.x);
    vec2 posZW = unpackHalf2x16(aPosHalf.y);

    SetCommonVSOut();

    gl_Position = uInstanceDataBuffer[gBaseInstance + gInstanceID].Matrix * vec4(posXY, posZW);
}

