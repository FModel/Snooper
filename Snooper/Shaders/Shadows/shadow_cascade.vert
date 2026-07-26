// Depth-only vertex stage for the shadow cascades. It shares the deformation hooks with
// Buffers/CommonMesh.vert so spline and skinned meshes cast the shape they render as, but
// MESH_DEPTH_ONLY compiles out everything that only shading needs.

#define MESH_VERTEX_STAGE
#define MESH_DEPTH_ONLY

layout (location = 0) in uvec2 aPosHalf;

uniform mat4 uViewProjection;

#include "Buffers/PerInstanceData.glsl"
#include "Buffers/MeshHooks.glsl"

void main()
{
    vec2 posXY = unpackHalf2x16(aPosHalf.x);
    vec2 posZW = unpackHalf2x16(aPosHalf.y);

    MeshVertex vertex;
    vertex.Position = vec4(posXY, posZW);
    vertex.Normal = vec4(0.0);
    vertex.Tangent = vec3(0.0);

    int id = gl_BaseInstance + gl_InstanceID;
    DeformVertex(uDrawDataBuffer[gl_DrawID], id, vertex);

    gl_Position = uViewProjection * (uInstanceDataBuffer[id].Matrix * vertex.Position);
}
