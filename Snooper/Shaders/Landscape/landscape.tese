#extension GL_ARB_bindless_texture : require

layout (quads, fractional_odd_spacing, ccw) in;

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/PerInstanceData.glsl"

struct PerMaterialData
{
    bool IsReady;
    uint WeightmapCount;

    sampler2D Heightmap;
    sampler2D Weightmaps[4];
    uint EnabledChannels[4];

    vec2 HeightmapScaleBias;
    vec2 WeightmapScaleBias;

    // uint.MaxValue == no visibility layer on this tile
    uint VisibilityTextureIndex;
    uint VisibilityChannelIndex;
};

layout(std430, binding = 2) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

layout(std430, binding = 8) restrict readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

in flat uint tcInstanceID[];
in flat uint tcDrawID[];

uniform float uSizeQuads;
uniform float uQuadCount;
uniform float uGlobalScale;
uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out flat uint gDrawID;
out TE_OUT {
    vec3 vViewPos;
    mat3 TBN;
    float vHeight;
    vec2 vTessCoord;
} te_out;

void main()
{
    gDrawID = tcDrawID[0];

    te_out.vTessCoord = gl_TessCoord.xy;

    float u = te_out.vTessCoord.x;
    float v = te_out.vTessCoord.y;

    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;

    vec4 p0 = (p01 - p00) * u + p00;
    vec4 p1 = (p11 - p10) * u + p10;
    vec4 p = (p1 - p0) * v + p0;

    mat4 matrix = uInstanceDataBuffer[tcInstanceID[0]].Matrix;

    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];
    if (!materialData.IsReady)
    {
        te_out.vViewPos = vec3(0.0);
        te_out.TBN = mat3(uViewMatrix);
        te_out.vHeight = 0.0;
        gl_Position = uProjectionMatrix * uViewMatrix * matrix * p;
        return;
    }

    float quadFraction = 1.0 / uQuadCount;
    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;

    // Hole: if a visibility layer exists, sample it and discard the patch by
    // pushing the vertex out of clip space when the channel value > 0.5.
    if (materialData.VisibilityTextureIndex != 0xFFFFFFFFu)
    {
        sampler2D weightmap = materialData.Weightmaps[materialData.VisibilityTextureIndex];

        vec2 weightmapSize = textureSize(weightmap, 0);
        vec2 weightmapTexelSize = 1.0 / weightmapSize;
        vec2 weightmapUvSize = vec2(uSizeQuads) / weightmapSize;

        vec2 visUv = materialData.WeightmapScaleBias + subPatchOffset * weightmapUvSize + vec2(u, v) * (weightmapUvSize * quadFraction);
        visUv = visUv * (1.0 - weightmapTexelSize) + 0.5 * weightmapTexelSize;

        if (texture(weightmap, visUv)[materialData.VisibilityChannelIndex] > 0.5)
        {
            gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
            te_out.vViewPos = vec3(0.0);
            te_out.TBN = mat3(uViewMatrix);
            te_out.vHeight = 0.0;
            return;
        }
    }

    vec2 heightmapSize = textureSize(materialData.Heightmap, 0);
    vec2 heightmapTexelSize = 1.0 / heightmapSize;
    vec2 heightmapUvSize = vec2(uSizeQuads) / heightmapSize;

    vec2 uv = materialData.HeightmapScaleBias + subPatchOffset * heightmapUvSize + vec2(u, v) * (heightmapUvSize * quadFraction);
    uv = uv * (1.0 - heightmapTexelSize) + 0.5 * heightmapTexelSize;

    vec4 color = texture(materialData.Heightmap, uv);
    float R = color.r * 255.0;
    float G = color.g * 255.0;
    te_out.vHeight = ((R * 256.0) + G - 32768.0) / 128.0 * uGlobalScale;

    float nx = 2.0 * color.b - 1.0;
    float nz = 2.0 * color.a - 1.0;
    float ny = sqrt(1.0 - nx * nx + nz * nz);
    te_out.TBN = mat3(uViewMatrix) * mat3(normalize(vec3(-nz, 0.0, nx)), normalize(vec3(0.0, nz, -ny)), normalize(vec3(nx, ny, nz)));

    // displace point along normal
    vec4 normal = normalize(vec4(cross((p10 - p00).xyz, (p01 - p00).xyz), 0));
    p += normal * te_out.vHeight;

    vec4 viewPos = uViewMatrix * matrix * p;
    gl_Position = uProjectionMatrix * viewPos;
    te_out.vViewPos = viewPos.xyz;
}
