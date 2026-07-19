layout (location = 0) in vec3 aPos;

out VS_OUT {
    vec3 nearPoint;
    vec3 farPoint;
    vec3 cameraPosition;
} vs_out;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

vec3 UnprojectPoint(mat4 inverseViewProjection, vec2 xy, float z)
{
    vec4 unprojectedPoint = inverseViewProjection * vec4(xy, z, 1.0);
    return unprojectedPoint.xyz / unprojectedPoint.w;
}

void main()
{
    mat4 inverseView = inverse(uViewMatrix);
    mat4 inverseViewProjection = inverseView * inverse(uProjectionMatrix);

    vs_out.cameraPosition = inverseView[3].xyz;
    vs_out.nearPoint = UnprojectPoint(inverseViewProjection, aPos.xy, -1.0);
    vs_out.farPoint = UnprojectPoint(inverseViewProjection, aPos.xy, 1.0);

    gl_Position = vec4(aPos, 1.0);
}
