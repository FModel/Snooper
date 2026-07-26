// Vertex stage shared by every program MeshRenderSystem builds. Owns the vertex layout,
// the VS_OUT block and the built-in debug colour modes; variant-specific work goes through
// the hooks in Buffers/MeshHooks.glsl, which also owns Buffers/PerDrawData.glsl.

#define MESH_VERTEX_STAGE

layout (location = 0) in uvec2 aPosHalf;       // half2(pos.xy) | half2(pos.zw)
layout (location = 1) in uint  aNormalPacked;  // RGB10A2: bits 0-9=nx, 10-19=ny, 20-29=nz, 30-31=nw
layout (location = 2) in uint  aTangentPacked; // RGB10A2: bits 0-9=tx, 10-19=ty, 20-29=tz, 30-31=texLayer
layout (location = 3) in uint  aTexCoordsHalf; // half2(uv.xy) packed

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform uint uFragmentColorMode;

#include "Buffers/PerInstanceData.glsl"
#include "Buffers/PerMeshData.glsl"

layout(std430, binding = BINDING_VERTEX_COLORS) buffer PerVertexColorBuffer
{
    int uVertexColorBuffer[];
};

flat out uint vTexLayer;
flat out uint vColorMode;
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

#include "Buffers/MeshHooks.glsl"

void CommonMeshMain()
{
    vec2 posXY = unpackHalf2x16(aPosHalf.x);
    vec2 posZW = unpackHalf2x16(aPosHalf.y);

    MeshVertex vertex;
    vertex.Position = vec4(posXY, posZW);
    vertex.Normal = normalize(vec4(
        Unpack10Snorm(aNormalPacked),
        Unpack10Snorm(aNormalPacked >> 10u),
        Unpack10Snorm(aNormalPacked >> 20u),
        Unpack10Snorm(aNormalPacked >> 30u)));
    vertex.Tangent = normalize(vec3(
        Unpack10Snorm(aTangentPacked),
        Unpack10Snorm(aTangentPacked >> 10u),
        Unpack10Snorm(aTangentPacked >> 20u)));

    uint texLayer = (aTangentPacked >> 30u) & 3u; // 2-bit texLayer from tangent.w
    vec2 aTexCoords = unpackHalf2x16(aTexCoordsHalf);

    int id = gl_BaseInstance + gl_InstanceID;
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    PerDrawData draw = uDrawDataBuffer[gl_DrawID];

    DeformVertex(draw, id, vertex);

    vec4 viewPos = uViewMatrix * matrix * vertex.Position;
    gl_Position = uProjectionMatrix * viewPos;

    mat3 nMatrix = transpose(inverse(mat3(matrix)));
    vec3 T = normalize(nMatrix * vertex.Tangent);
    vec3 N = normalize(nMatrix * vertex.Normal.xyz);
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    vTexLayer = texLayer;
    vs_out.vViewPos = viewPos.xyz;
    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, cross(N, T) * vertex.Normal.w, N);

    vec3 color = GetVertexBaseColor(draw, id);
    vColorMode = uFragmentColorMode != 0 ? uFragmentColorMode : uMeshDataBuffer[draw.MeshIndex].ColorMode;

    if (!GetVertexDebugColor(draw, id, vColorMode, color))
    {
        if (vColorMode == 2) // ComponentId
        {
            color = hashColor(draw.PickingId);
        }
        else if (vColorMode == 3) // InstanceId
        {
            color = hashColor(uint(id));
        }
        else if (vColorMode == 4) // DrawId
        {
            color = hashColor(uint(gl_DrawID));
        }
        else if (vColorMode == 5 && draw.BaseColor != 0xFFFFFFFFu) // VertexColor
        {
            color = UnpackColor(uVertexColorBuffer[draw.BaseColor + (gl_VertexID - gl_BaseVertex)]).rgb;
        }
        else if (vColorMode == 8) // LODLevel
        {
            color = hashColor(draw.Lod);
        }
    }

    vs_out.vFragColor = color;
}
