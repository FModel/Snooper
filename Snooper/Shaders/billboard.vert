layout (location = 0) in vec2 aPos;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

#define DEFAULT_SCREEN_SIZE 0.075f

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"

out vec2 vTexCoords;

void main()
{
    SetCommonVSOut();

    int id = gBaseInstance + gInstanceID;
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    
    vec3 worldPos = matrix[3].xyz;
    vec4 viewPos = uViewMatrix * vec4(worldPos, 1.0);
    vec3 right = vec3(uViewMatrix[0][0], uViewMatrix[1][0], uViewMatrix[2][0]);
    vec3 up = vec3(uViewMatrix[0][1], uViewMatrix[1][1], uViewMatrix[2][1]);
    float screenSize = min(DEFAULT_SCREEN_SIZE * length(viewPos.xyz), 5.0);
    
    vec3 billboard = worldPos + right * aPos.x * screenSize + up * -aPos.y * screenSize;
    gl_Position = uProjectionMatrix * uViewMatrix * vec4(billboard, 1.0);

    vTexCoords = aPos * 0.5 + 0.5;
}