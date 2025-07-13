layout (location = 0) in vec3 aPos;

out VS_OUT {
    vec3 nearPoint;
    vec3 farPoint;
    mat4 proj;
    mat4 view;
    float near;
    float far;
} vs_out;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform float uNear;
uniform float uFar;

vec3 UnprojectPoint(vec2 xy, float z)
{
    mat4 viewInv = inverse(uViewMatrix);
    mat4 projInv = inverse(uProjectionMatrix);
    vec4 unprojectedPoint =  viewInv * projInv * vec4(xy, z, 1.0);
    return unprojectedPoint.xyz / unprojectedPoint.w;
}

void main()
{
    vs_out.near = uNear;
    vs_out.far  = uFar;
    vs_out.proj = uProjectionMatrix;
    vs_out.view = uViewMatrix;
    vs_out.nearPoint = UnprojectPoint(aPos.xy, -1.0).xyz;
    vs_out.farPoint  = UnprojectPoint(aPos.xy, 1.0).xyz;
    
    gl_Position = vec4(aPos, 1.0);
}