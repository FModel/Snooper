// Spline mesh deformation, selected by SPLINE_VERTEX.
// Pulled in by Buffers/MeshHooks.glsl, so it reaches CommonMesh.vert and
// Shadows/shadow_cascade.vert alike. Owns Buffers/PerSplineData.glsl and requires
// Buffers/PerDrawData.glsl to be included first, which MeshHooks.glsl does.

#if defined(MESH_VERTEX_STAGE)

#include "Buffers/PerSplineData.glsl"

// Position only: the slice transform is not applied to the tangent basis.
void SplineDeformVertex(PerDrawStatic draw, int instance, inout MeshVertex v)
{
    vec3 uePos = v.Position.xzy;
    SplineMeshParams params = uSplineParameters[instance];
    float distanceAlong = GetAxisValueRef(params.ForwardAxis, uePos);
    vec3 computed = ComputeRatioAlongSpline(params, distanceAlong);
    mat4 sliceTransform = CalcSliceTransformAtSplineOffset(params, computed);
    SetAxisValueRef(params.ForwardAxis, uePos, 0.0);
    v.Position = (sliceTransform * vec4(uePos, 1.0)).xzyw;
}

#endif
