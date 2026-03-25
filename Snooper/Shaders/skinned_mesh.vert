layout(std430, binding = 3) buffer PerBonePoseBuffer
{
    mat4 uPoseBuffer[]; // current pose matrices (bind pose or animated pose)
};

layout(std430, binding = 4) buffer PerBoneInverseBindBuffer
{
    mat4 uInverseBindBuffer[]; // inverse bind pose matrices
};

layout(std430, binding = 6) buffer PerVertexBoneInfluenceBuffer
{
    uint uVertexBoneInfluenceBuffer[]; // upper 16 bits = boneId, lower 16 bits = rawWeight
};

layout(std430, binding = 7) buffer PerVertexBoneInfluenceOffsetBuffer
{
    uint uVertexBoneInfluenceOffsetBuffer[];
};

uvec2 unpackBoneInfluence(uint boneInfluence)
{
    return uvec2(boneInfluence >> 16, boneInfluence & 0xFFFFu);
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

vec3 heatmap(float t)
{
    float s = t * 4.0;
    float r = clamp(s - 2.0, 0.0, 1.0);
    float g = clamp(s, 0.0, 1.0) - clamp(s - 3.0, 0.0, 1.0);
    float b = 1.0 - clamp(s - 1.0, 0.0, 1.0);
    return vec3(r, g, b);
}

#define SKINNED_MESH_VERTEX
#include "Buffers/CommonMesh.vert"
#include "Buffers/common.vert"

void main()
{
    SetCommonVSOut();
    CommonMeshMain();
}
