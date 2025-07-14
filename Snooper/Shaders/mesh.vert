layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec3 aTangent;
layout (location = 3) in vec2 aTexCoords;

struct PerInstanceData
{
    mat4 Matrix;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform int uDebugColorMode;

out flat int mIndex;
out VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} vs_out;

void main()
{
    int id = gl_BaseInstance + gl_InstanceID;
    mIndex = gl_DrawID;
    
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    vec4 viewPos = uViewMatrix * matrix * vec4(aPos, 1.0);
    gl_Position = uProjectionMatrix * viewPos;

    vec3 T = normalize(vec3(matrix * vec4(aTangent,   0.0)));
    vec3 N = normalize(vec3(matrix * vec4(aNormal,    0.0)));
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    vs_out.vViewPos = viewPos.xyz;
    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, normalize(cross(N, T)), N);

    vs_out.vDebugColor = vec3(0.75);
    if (uDebugColorMode == 0) return;
    else if (uDebugColorMode == 1)
    {
        id = gl_BaseVertex;
    }
    else if (uDebugColorMode == 3)
    {
        id = gl_DrawID;
    }

    vs_out.vDebugColor = mix(vec3(0.25), vec3(1.0), vec3(
        float((id * 97u) % 255u) / 255.0,
        float((id * 59u) % 255u) / 255.0,
        float((id * 31u) % 255u) / 255.0
    ));
}