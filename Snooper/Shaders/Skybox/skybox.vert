#include "Lighting/scene_data.glsl"

uniform mat4 uInverseProjectionMatrix;

out vec3 vTexCoords;

void main()
{
    // one triangle that covers the screen, from the vertex index alone
    vec2 position = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2) * 2.0 - 1.0;

    vec4 view = uInverseProjectionMatrix * vec4(position, 1.0, 1.0);
    vTexCoords = mat3(uInverseViewMatrix) * (view.xyz / view.w);

    gl_Position = vec4(position, 0.0, 1.0);
}
