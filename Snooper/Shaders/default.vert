layout (location = 0) in vec3 aPos;

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

void main()
{
    SetCommonVSOut();
    
    gl_Position = uProjectionMatrix * uViewMatrix * uInstanceDataBuffer[gBaseInstance + gInstanceID].Matrix * vec4(aPos, 1.0);
}