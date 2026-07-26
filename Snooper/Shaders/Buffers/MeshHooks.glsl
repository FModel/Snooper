// Hook contract for the mesh pipeline: mesh.vert, geometry.frag, mesh.frag and
// Shadows/shadow_cascade.vert all funnel through here.
//
// A variant render system passes its selector define (e.g. SKINNED_MESH_VERTEX) to the
// MeshRenderSystem ctor; every program the system builds receives it, so the registry
// below pulls the variant's implementation file into all of them at once.
//
// Two hook shapes:
//   chained      - the variant defines a uniquely named function and the dispatcher below
//                  calls each enabled one in turn, so variants compose (DeformVertex,
//                  GetVertexDebugColor).
//   single-claim - the variant defines OVERRIDE_<HOOK> and supplies the body, replacing
//                  the default (GetVertexBaseColor, GetSurfaceColor). Two claimants is a
//                  redefinition error, which is the failure we want.
//
// Stage macros are set by the including header, never by a variant:
//   MESH_VERTEX_STAGE     vertex stage
//   MESH_FRAGMENT_STAGE   fragment stage
//   MESH_DEPTH_ONLY       vertex stage, shadow pass: position only, no shading hooks
//
// Every hook file must guard its contents by stage, because the registry only knows which
// variants are enabled, not which stage is being compiled.
//
// Include this after the stage header has declared vs_out / fs_in and, in the fragment
// stage, after material_sampling.glsl. Owns Buffers/PerDrawData.glsl: nothing that pulls
// this file in may include that one again, as EmbeddedShader.ResolveIncludes has no
// include guards.

#include "Buffers/PerDrawData.glsl"

#if defined(MESH_VERTEX_STAGE)

struct MeshVertex
{
    vec4 Position; // object space
    vec4 Normal;   // .w carries the tangent basis sign
    vec3 Tangent;
};

// Deform the vertex in object space, before the instance matrix is applied.
// Under MESH_DEPTH_ONLY only Position is meaningful.
void DeformVertex(PerDrawData draw, int instance, inout MeshVertex v);

#if !defined(MESH_DEPTH_ONLY)
// Base colour for vs_out.vFragColor, used as-is when no debug mode is active.
vec3 GetVertexBaseColor(PerDrawData draw, int instance);

// Extra uFragmentColorMode entries. Return true to claim the mode, which keeps the
// built-in chain from running.
bool GetVertexDebugColor(PerDrawData draw, int instance, uint mode, inout vec3 color);
#endif

#endif

#if defined(MESH_FRAGMENT_STAGE)
// Final surface colour. vertexColor is the interpolated vs_out.vFragColor, which is how a
// variant hands per-vertex work to the fragment stage without touching the VS_OUT block.
vec3 GetSurfaceColor(PerDrawData draw, PerMaterialData material, LayerData layer, vec3 vertexColor);
#endif

// ------------------------------------------------------------------ variant registry
#if defined(SPLINE_VERTEX)
#include "Hooks/spline.glsl"
#endif
#if defined(SKINNED_MESH_VERTEX)
#include "Hooks/skinning.glsl"
#endif

// --------------------------------------------------------- dispatchers and defaults
#if defined(MESH_VERTEX_STAGE)

void DeformVertex(PerDrawData draw, int instance, inout MeshVertex v)
{
#if defined(SKINNED_MESH_VERTEX)
    SkinDeformVertex(draw, instance, v); // skin to the current pose first
#endif
#if defined(SPLINE_VERTEX)
    SplineDeformVertex(draw, instance, v); // then bend the result along the spline
#endif
}

#if !defined(MESH_DEPTH_ONLY)

#if !defined(OVERRIDE_VERTEX_BASE_COLOR)
vec3 GetVertexBaseColor(PerDrawData draw, int instance)
{
    return vec3(0.5); // Clay
}
#endif

bool GetVertexDebugColor(PerDrawData draw, int instance, uint mode, inout vec3 color)
{
#if defined(SKINNED_MESH_VERTEX)
    if (SkinVertexDebugColor(draw, instance, mode, color)) return true;
#endif
    return false;
}

#endif
#endif

#if defined(MESH_FRAGMENT_STAGE) && !defined(OVERRIDE_SURFACE_COLOR)
vec3 GetSurfaceColor(PerDrawData draw, PerMaterialData material, LayerData layer, vec3 vertexColor)
{
    return layer.diffuse.rgb;
}
#endif
