layout (location = 0) in uvec2 aPosHalf;

#include "Buffers/PerInstanceData.glsl"

void main()
{
    vec2 posXY = unpackHalf2x16(aPosHalf.x);
    vec2 posZW = unpackHalf2x16(aPosHalf.y);

    // TODO: alter pos for skinning and splines

    gl_Position = uInstanceDataBuffer[gl_BaseInstance + gl_InstanceID].Matrix * vec4(posXY, posZW);
}
