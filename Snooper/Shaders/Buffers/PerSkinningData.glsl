layout(std430, binding = BINDING_SKIN_POSES) buffer PerBonePoseBuffer
{
    mat4 uPoseBuffer[]; // current pose matrices (bind pose or animated pose)
};

layout(std430, binding = BINDING_SKIN_INVERSE_BIND) buffer PerBoneInverseBindBuffer
{
    mat4 uInverseBindBuffer[]; // inverse bind pose matrices
};

layout(std430, binding = BINDING_SKIN_BONE_INFLUENCES) buffer PerVertexBoneInfluenceBuffer
{
    uint uVertexBoneInfluenceBuffer[]; // upper 16 bits = boneId, lower 16 bits = rawWeight
};

layout(std430, binding = BINDING_SKIN_BONE_INFLUENCE_OFFSETS) buffer PerVertexBoneInfluenceOffsetBuffer
{
    uint uVertexBoneInfluenceOffsetBuffer[];
};

struct PerMeshSkinningData // one entry per unique mesh, index-aligned with PerMeshData
{
    uint BaseBone; // offset of this mesh's bones in the inverse bind buffer
    uint LOD_BaseBoneInfluence[8]; // Settings.MaxNumberOfLods
    uint Pad0;
    uint Pad1;
    uint Pad2;
};

layout(std430, binding = BINDING_SKIN_MESH_DATA) readonly buffer PerMeshSkinningDataBuffer
{
    PerMeshSkinningData uSkinMeshDataBuffer[];
};

layout(std430, binding = BINDING_SKIN_POSE_MAPPING) readonly buffer PerComponentPoseOffsetBuffer
{
    uint uPoseOffsetByComponent[]; // componentId -> base pose index
};

// requires Buffers/PerDrawData.glsl to be included first
void getSkinningBases(PerDrawData draw, out uint baseBone, out uint basePose, out uint baseInfluence)
{
    PerMeshSkinningData skin = uSkinMeshDataBuffer[draw.MeshIndex];
    baseBone = skin.BaseBone;
    basePose = uPoseOffsetByComponent[draw.PickingId];
    baseInfluence = skin.LOD_BaseBoneInfluence[draw.Lod];
}

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
