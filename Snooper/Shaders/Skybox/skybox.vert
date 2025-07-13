layout (location = 0) in vec3 aPos;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out vec3 vTexCoords;

void main()
{
    vTexCoords = aPos;
    gl_Position = (uProjectionMatrix * uViewMatrix * vec4(aPos, 1.0)).xyww;
}