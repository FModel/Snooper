// Skeletal mesh skinning, selected by SKINNED_MESH_VERTEX.
// Pulled in by Buffers/MeshHooks.glsl, so it reaches CommonMesh.vert and
// Shadows/shadow_cascade.vert alike. Owns Buffers/PerSkinningData.glsl and requires
// Buffers/PerDrawData.glsl to be included first, which MeshHooks.glsl does.

#if defined(MESH_VERTEX_STAGE)

#include "Buffers/PerSkinningData.glsl"

void SkinDeformVertex(PerDrawData draw, int instance, inout MeshVertex v)
{
    uint baseBone, basePose, baseInfluence;
    getSkinningBases(draw, uint(instance), baseBone, basePose, baseInfluence);

    uint packedInfluenceOffset = uVertexBoneInfluenceOffsetBuffer[baseInfluence + (gl_VertexID - gl_BaseVertex)];
    uint startIndex = packedInfluenceOffset >> 8;
    uint count = packedInfluenceOffset & 0xFFu;

    vec4 pos = v.Position;
    vec4 uePos = vec4(0.0);
#if !defined(MESH_DEPTH_ONLY)
    vec4 normal = v.Normal;
    vec3 tangent = v.Tangent;
    vec4 ueNormal = vec4(0.0);
    vec3 ueTangent = vec3(0.0);
#endif

    for (uint i = 0u; i < count; i++)
    {
        uvec2 inf = unpackBoneInfluence(uVertexBoneInfluenceBuffer[startIndex + i]);
        uint boneIndex = inf.x;
        float weight = float(inf.y) / 255.0;

        mat4 skinningMatrix = uPoseBuffer[basePose + boneIndex] * uInverseBindBuffer[baseBone + boneIndex];
        uePos += skinningMatrix * pos * weight;
#if !defined(MESH_DEPTH_ONLY)
        ueNormal += skinningMatrix * normal * weight;
        ueTangent += mat3(skinningMatrix) * tangent * weight;
#endif
    }

    v.Position = uePos;
#if !defined(MESH_DEPTH_ONLY)
    v.Normal = ueNormal;
    v.Tangent = ueTangent;
#endif
}

#if !defined(MESH_DEPTH_ONLY)
// Mode 7: BoneInfluences / bone weight painting. The bases are refetched because the
// deformation above keeps them local to itself; this is a debug-only path.
bool SkinVertexDebugColor(PerDrawData draw, int instance, uint mode, inout vec3 color)
{
    if (mode != 7) return false;

    uint baseBone, basePose, baseInfluence;
    getSkinningBases(draw, uint(instance), baseBone, basePose, baseInfluence);

    uint packedInfluenceOffset = uVertexBoneInfluenceOffsetBuffer[baseInfluence + (gl_VertexID - gl_BaseVertex)];
    uint startIndex = packedInfluenceOffset >> 8;
    uint count = packedInfluenceOffset & 0xFFu;

    if (count == 0u)
    {
        color = vec3(0.0);
    }
    else
    {
        uint bone; float weight;
        dominantInfluence(startIndex, count, bone, weight);
        color = heatmap(weight);
    }

    return true;
}
#endif

#endif
