layout (location = 0) in vec3 aPos;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out vec3 vTexCoords;

void main()
{
    vTexCoords = aPos;
    vec4 clip = uProjectionMatrix * uViewMatrix * vec4(aPos, 1.0);
    gl_Position = vec4(clip.xy, 0.0, clip.w); // reversed-Z: the far plane sits at depth 0
}