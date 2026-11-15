layout (location = 0) in vec3 aPos;

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out vec3 vViewPos;

void main()
{
    SetCommonVSOut();

    mat4 matrix = uInstanceDataBuffer[gl_BaseInstance + gl_InstanceID].Matrix;
    vec4 viewPos = uViewMatrix * matrix * vec4(aPos, 1.0);
    vViewPos = viewPos.xyz;

    gl_Position = uProjectionMatrix * viewPos;
}
