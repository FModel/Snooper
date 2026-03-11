layout (location = 0) in uvec2 aPosHalf;       // half2(pos.xy) | half2(pos.zw)
layout (location = 1) in uint  aNormalPacked;  // RGB10A2: bits 0-9=nx, 10-19=ny, 20-29=nz, 30-31=texLayer
layout (location = 2) in uint  aTangentPacked; // RGB10A2: bits 0-9=tx, 10-19=ty, 20-29=tz, 30-31=unused
layout (location = 3) in uint  aTexCoordsHalf; // half2(uv.xy) packed

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform int uFragmentColorMode;

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/PerInstanceData.glsl"

layout(std430, binding = 3) buffer PerVertexColorBuffer
{
    int uVertexColorBuffer[];
};

layout(std430, binding = 4) buffer PerVertexBoneInfluenceBuffer
{
    uint uVertexBoneInfluenceBuffer[]; // upper 16 bits = boneId, lower 16 bits = rawWeight
};

layout(std430, binding = 5) buffer PerVertexBoneInfluenceOffsetBuffer
{
    uint uVertexBoneInfluenceOffsetBuffer[];
};

flat out uint vTexLayer;
out VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vFragColor;
} vs_out;

vec4 UnpackColor(int color)
{
    float a = float((color >> 24) & 0xFF);
    float r = float((color >> 16) & 0xFF);
    float g = float((color >> 8) & 0xFF);
    float b = float((color >> 0) & 0xFF);
    return vec4(r, g, b, a) / 255.0;
}

uvec2 unpackBoneInfluence(uint boneInfluence)
{
    return uvec2(boneInfluence >> 16, boneInfluence & 0xFFFFu);
}

vec3 hashColor(uint id)
{
    return mix(vec3(0.2), vec3(1.0), vec3(
        float((id * 97u) % 255u) / 255.0,
        float((id * 59u) % 255u) / 255.0,
        float((id * 31u) % 255u) / 255.0));
}

vec3 heatmap(float t)
{
    return vec3(
        clamp(4.0 * t - 2.0,            0.0, 1.0),
        clamp(1.0 - abs(4.0 * t - 2.0), 0.0, 1.0),
        clamp(2.0 - 4.0 * t,            0.0, 1.0));
}

void dominantInfluence(uint startIndex, uint count, out uint bone, out float weight)
{
    bone   = 0u;
    weight = 0.0;
    uint dominantRaw = 0u;
    for (uint i = 0u; i < count; i++)
    {
        uvec2 inf = unpackBoneInfluence(uVertexBoneInfluenceBuffer[startIndex + i]);
        if (inf.y > dominantRaw)
        {
            dominantRaw = inf.y;
            bone        = inf.x;
            weight      = float(inf.y) / 255.0;
        }
    }
}

float Unpack10Snorm(uint bits)
{
    int i = int(bits & 0x3FFu);
    if (i >= 512) i -= 1024; // sign-extend from 10 bits
    return clamp(float(i) / 511.0, -1.0, 1.0);
}

void CommonMeshMain()
{
    vec2 posXY = unpackHalf2x16(aPosHalf.x);
    vec2 posZW = unpackHalf2x16(aPosHalf.y);
    vec4 aPos  = vec4(posXY, posZW);

    vec3 aNormal  = normalize(vec3(
        Unpack10Snorm(aNormalPacked),
        Unpack10Snorm(aNormalPacked >> 10u),
        Unpack10Snorm(aNormalPacked >> 20u)));
    vec3 aTangent = normalize(vec3(
        Unpack10Snorm(aTangentPacked),
        Unpack10Snorm(aTangentPacked >> 10u),
        Unpack10Snorm(aTangentPacked >> 20u)));
    uint texLayer = (aNormalPacked >> 30u) & 3u; // 2-bit texLayer from normal.w

    vec2 aTexCoords = unpackHalf2x16(aTexCoordsHalf);

    int id = gl_BaseInstance + gl_InstanceID;
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gl_DrawID];

#ifdef SPLINE_VERTEX
    vec3 uePos = aPos.xzy;
    SplineMeshParams params = uSplineParameters[uSplineIdToParameterIndex[cmd.PickingId]];
    float distanceAlong = GetAxisValueRef(params.ForwardAxis, uePos);
    vec3 computed = ComputeRatioAlongSpline(params, distanceAlong);
    mat4 sliceTransform = CalcSliceTransformAtSplineOffset(params, computed);
    SetAxisValueRef(params.ForwardAxis, uePos, 0.0);

    vec4 viewPos = uViewMatrix * matrix * (sliceTransform * vec4(uePos, 1.0)).xzyw;
#else
    vec4 viewPos = uViewMatrix * matrix * aPos;
#endif

    gl_Position = uProjectionMatrix * viewPos;

    mat3 nMatrix = transpose(inverse(mat3(matrix)));
    vec3 T = normalize(nMatrix * aTangent);
    if (determinant(nMatrix) < 0.0) // flipped normals
    {
        T = -T;
    }
    vec3 N = normalize(nMatrix * aNormal);
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    vTexLayer = texLayer;
    vs_out.vViewPos = viewPos.xyz;
    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, cross(N, T), N);
    vs_out.vFragColor = vec3(0.5); // Clay

    int mode = uFragmentColorMode;
    if (mode == 2) // ComponentId
    {
        vs_out.vFragColor = hashColor(cmd.PickingId);
    }
    else if (mode == 3) // InstanceId
    {
        vs_out.vFragColor = hashColor(uint(gl_BaseInstance + gl_InstanceID));
    }
    else if (mode == 4) // DrawId
    {
        vs_out.vFragColor = hashColor(uint(gl_DrawID));
    }
    else if (mode == 5 && cmd.BaseColor != 0xFFFFFFFFu) // VertexColor
    {
        vs_out.vFragColor = UnpackColor(uVertexColorBuffer[cmd.BaseColor + (gl_VertexID - gl_BaseVertex)]).rgb;
    }
    else if ((mode == 7 || mode == 8) && cmd.BaseBoneInfluence != 0xFFFFFFFFu)
    {
        uint influenceOffset = uVertexBoneInfluenceOffsetBuffer[cmd.BaseBoneInfluence + (gl_VertexID - gl_BaseVertex)];
        uint bone; float weight;
        dominantInfluence(influenceOffset >> 8, influenceOffset & 0xFFu, bone, weight);

        vs_out.vFragColor = mode == 8 ? heatmap(weight) : hashColor(bone);
    }
}
