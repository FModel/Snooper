layout (location = 0) in uvec2 aPosHalf;       // half2(pos.xy) | half2(pos.zw)
layout (location = 1) in uint  aNormalPacked;  // RGB10A2: bits 0-9=nx, 10-19=ny, 20-29=nz, 30-31=texLayer
layout (location = 2) in uint  aTangentPacked; // RGB10A2: bits 0-9=tx, 10-19=ty, 20-29=tz, 30-31=unused
layout (location = 3) in uint  aTexCoordsHalf; // half2(uv.xy) packed

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform int uFragmentColorMode;

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/PerInstanceData.glsl"

layout(std430, binding = 5) buffer PerVertexColorBuffer
{
    int uVertexColorBuffer[];
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

vec3 hashColor(uint id)
{
    uint x = id * 747796405u + 2891336453u;
    x = ((x >> ((x >> 28u) + 4u)) ^ x) * 277803737u;
    x = (x >> 22u) ^ x;

    float h = float(x % 360u) / 60.0;
    float r = clamp(abs(h - 3.0) - 1.0, 0.0, 1.0);
    float g = clamp(2.0 - abs(h - 2.0), 0.0, 1.0);
    float b = clamp(2.0 - abs(h - 4.0), 0.0, 1.0);
    return vec3(r, g, b);
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

#if defined(SPLINE_VERTEX)
    vec3 uePos = aPos.xzy;
    SplineMeshParams params = uSplineParameters[uSplineIdToParameterIndex[cmd.PickingId]];
    float distanceAlong = GetAxisValueRef(params.ForwardAxis, uePos);
    vec3 computed = ComputeRatioAlongSpline(params, distanceAlong);
    mat4 sliceTransform = CalcSliceTransformAtSplineOffset(params, computed);
    SetAxisValueRef(params.ForwardAxis, uePos, 0.0);
    aPos = (sliceTransform * vec4(uePos, 1.0)).xzyw;
#elif defined(SKINNED_MESH_VERTEX)
    uint packedInfluenceOffset = uVertexBoneInfluenceOffsetBuffer[cmd.BaseBoneInfluence + (gl_VertexID - gl_BaseVertex)];
    uint startIndex = packedInfluenceOffset >> 8;
    uint count = packedInfluenceOffset & 0xFFu;

    vec4 uePos = vec4(0.0);
    vec3 ueNormal = vec3(0.0);
    vec3 ueTangent = vec3(0.0);

    for (uint i = 0u; i < count; i++)
    {
        uvec2 inf = unpackBoneInfluence(uVertexBoneInfluenceBuffer[startIndex + i]);
        uint boneIndex = inf.x;
        float weight = float(inf.y) / 255.0;

        mat4 skinningMatrix = uPoseBuffer[cmd.BasePose + boneIndex] * uInverseBindBuffer[cmd.BaseBone + boneIndex];
        uePos += skinningMatrix * aPos * weight;
        ueNormal += mat3(skinningMatrix) * aNormal * weight;
        ueTangent += mat3(skinningMatrix) * aTangent * weight;
    }

    aPos = uePos;
    aNormal = ueNormal;
    aTangent = ueTangent;
#endif

    vec4 viewPos = uViewMatrix * matrix * aPos;
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
#if defined(SKINNED_MESH_VERTEX)
    else if (mode == 7) // BoneInfluences / BoneWeightPainting
    {
        uint packedInfluenceOffset = uVertexBoneInfluenceOffsetBuffer[cmd.BaseBoneInfluence + (gl_VertexID - gl_BaseVertex)];
        uint startIndex = packedInfluenceOffset >> 8;
        uint count = packedInfluenceOffset & 0xFFu;

        if (count == 0u)
        {
            vs_out.vFragColor = vec3(0.0);
        }
        else
        {
            uint bone; float weight;
            dominantInfluence(startIndex, count, bone, weight);
            vs_out.vFragColor = heatmap(weight);
        }
    }
#endif
}
