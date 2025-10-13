layout (location = 0) in vec3 aPos;

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

#ifdef USE_GEOMETRY_SHADER
out flat uint vDrawID;
#endif

void main()
{
    SetCommonVSOut();
    
#ifdef USE_GEOMETRY_SHADER
    vDrawID = gDrawID;
#endif
    
    gl_Position = uProjectionMatrix * uViewMatrix * uInstanceDataBuffer[gBaseInstance + gInstanceID].Matrix * vec4(aPos, 1.0);
}