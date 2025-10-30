layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoords;

#include "Buffers/PerInstanceData.glsl"
#include "Buffers/common.vert"

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out vec2 vTexCoord;

void main()
{
    SetCommonVSOut();

    int id = gl_BaseInstance + gl_InstanceID;
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    
    gl_Position = uProjectionMatrix * uViewMatrix * matrix * vec4(aPos.x, 0.0, aPos.y, 1.0);
    vTexCoord = aTexCoords;
}