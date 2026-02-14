layout (location = 0) in vec3 aPos;

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"

void main()
{
    SetCommonVSOut();

    gl_Position = uInstanceDataBuffer[gBaseInstance + gInstanceID].Matrix * vec4(aPos, 1.0);
}

