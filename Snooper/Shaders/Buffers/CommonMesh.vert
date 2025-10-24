layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec3 aTangent;
layout (location = 3) in vec2 aTexCoords;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform int uDebugColorMode;

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/PerInstanceData.glsl"

layout(std430, binding = 5) buffer PerVertexColorBuffer
{
    int uVertexColorBuffer[];
};

layout(std430, binding = 6) buffer PerExtraUvBuffer
{
    vec2 uExtraUvBuffer[];
};

out VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    vec4 vColor;
    vec2 vExtraTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} vs_out;

vec4 UnpackColor(int color)
{
    float a = float((color >> 24) & 0xFF);
    float r = float((color >> 16) & 0xFF);
    float g = float((color >> 8) & 0xFF);
    float b = float((color >> 0) & 0xFF);
    return vec4(r, g, b, a) / 255.0;
}

void CommonMeshMain()
{
    int id = gl_BaseInstance + gl_InstanceID;
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    
#ifdef SPLINE_VERTEX
    vec3 uePos = aPos.xzy;
    SplineMeshParams params = uSplineParameters[gl_DrawID];
    float distanceAlong = GetAxisValueRef(params.ForwardAxis, uePos);
    vec3 computed = ComputeRatioAlongSpline(params, distanceAlong);
    mat4 sliceTransform = CalcSliceTransformAtSplineOffset(params, computed);
    SetAxisValueRef(params.ForwardAxis, uePos, 0.0);

    vec4 viewPos = uViewMatrix * matrix * (sliceTransform * vec4(uePos, 1.0)).xzyw;
#else
    vec4 viewPos = uViewMatrix * matrix * vec4(aPos, 1.0);
#endif

    gl_Position = uProjectionMatrix * viewPos;

    mat3 nMatrix = transpose(inverse(mat3(matrix)));
    vec3 T = normalize(vec3(vec4(nMatrix * aTangent, 0.0)));
    vec3 N = normalize(vec3(vec4(nMatrix * aNormal, 0.0)));
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gl_DrawID];
    
    vs_out.vViewPos = viewPos.xyz;
    vs_out.vTexCoords = aTexCoords;
    if (cmd.BaseColor != 0xFFFFFFFFu)
    {
        vs_out.vColor = UnpackColor(uVertexColorBuffer[cmd.BaseColor + (gl_VertexID - gl_BaseVertex)]);
    }
    else
    {
        vs_out.vColor = vec4(vec3(0.3333), 1.0);
    }
    if (cmd.BaseExtraUv == 0xFFFFFFFFu)
    {
        vs_out.vExtraTexCoords = uExtraUvBuffer[cmd.BaseExtraUv + (gl_VertexID - gl_BaseVertex)];
    }
    else
    {
        vs_out.vExtraTexCoords = vec2(0.0);
    }
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
