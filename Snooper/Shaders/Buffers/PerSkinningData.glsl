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

struct MorphDelta // one per (vertex, morph) pair, mirrors Descriptors/MorphDescriptor.cs
{
    uint MorphIndex;
    uint PositionXY;
    uint PositionZ_TangentX;
    uint TangentYZ;
};

layout(std430, binding = BINDING_MORPH_DELTAS) readonly buffer PerVertexMorphDeltaBuffer
{
    MorphDelta uMorphDeltaBuffer[]; // grouped by vertex: every morph touching a vertex sits in one run
};

layout(std430, binding = BINDING_MORPH_DELTA_OFFSETS) readonly buffer PerVertexMorphDeltaOffsetBuffer
{
    uint uMorphDeltaOffsetBuffer[]; // CSR prefix sum, vertex v owns deltas [offset[v], offset[v + 1])
};

layout(std430, binding = BINDING_MORPH_WEIGHTS) readonly buffer PerMorphWeightBuffer
{
    float uMorphWeightBuffer[]; // one weight per morph target, one contiguous set per component
};

struct PerMeshSkinningData // one entry per unique mesh, index-aligned with PerMeshData
{
    uint BaseBone; // offset of this mesh's bones in the inverse bind buffer
    uint MorphCount; // number of morph targets on this mesh, 0 when it has none
    uint LOD_BaseBoneInfluence[MAX_NUMBER_OF_LODS];
    uint LOD_BaseMorphOffset[MAX_NUMBER_OF_LODS];
    uint Pad0;
    uint Pad1;
};

layout(std430, binding = BINDING_SKIN_MESH_DATA) readonly buffer PerMeshSkinningDataBuffer
{
    PerMeshSkinningData uSkinMeshDataBuffer[];
};

struct PerInstanceSkinningData // one per instance, indexed by gl_BaseInstance + gl_InstanceID
{
    uint BasePose;
    uint BaseMorphWeight; // 0xFFFFFFFF when the mesh has no morph targets
};

layout(std430, binding = BINDING_SKIN_INSTANCE_DATA) readonly buffer PerInstanceSkinningDataBuffer
{
    PerInstanceSkinningData uSkinInstanceBuffer[];
};

// requires Buffers/PerDrawData.glsl to be included first
void getSkinningBases(PerDrawStatic draw, PerDrawCulled culled, uint instance, out uint baseBone, out uint basePose, out uint baseInfluence)
{
    PerMeshSkinningData skin = uSkinMeshDataBuffer[draw.MeshIndex];
    baseBone = skin.BaseBone;
    basePose = uSkinInstanceBuffer[instance].BasePose;
    baseInfluence = skin.LOD_BaseBoneInfluence[culled.Lod];
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
