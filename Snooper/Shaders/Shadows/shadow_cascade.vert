layout (location = 0) in uvec2 aPosHalf;

uniform mat4 uViewProjection;

#include "Buffers/PerInstanceData.glsl"
#include "Buffers/PerDrawCommand.glsl"
#if defined(SPLINE_VERTEX)
#include "Buffers/PerSplineData.glsl"
#elif defined(SKINNED_MESH_VERTEX)
#include "Buffers/PerSkinningData.glsl"
#endif

void main()
{
    vec2 posXY = unpackHalf2x16(aPosHalf.x);
    vec2 posZW = unpackHalf2x16(aPosHalf.y);
    vec4 aPos  = vec4(posXY, posZW);

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

    for (uint i = 0u; i < count; i++)
    {
        uvec2 inf = unpackBoneInfluence(uVertexBoneInfluenceBuffer[startIndex + i]);
        uint boneIndex = inf.x;
        float weight = float(inf.y) / 255.0;

        mat4 skinningMatrix = uPoseBuffer[cmd.BasePose + boneIndex] * uInverseBindBuffer[cmd.BaseBone + boneIndex];
        uePos += skinningMatrix * aPos * weight;
    }

    aPos = uePos;
#endif

    gl_Position = uViewProjection * (uInstanceDataBuffer[gl_BaseInstance + gl_InstanceID].Matrix * aPos);
}
