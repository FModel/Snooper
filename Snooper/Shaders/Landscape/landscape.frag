#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;
layout (location = 4) out uint gPicking;

#include "Buffers/bindless.glsl"
#include "Buffers/Wireframe.glsl"

struct PerMaterialData
{
    bool IsReady;
    uint WeightmapCount;

    TEXTURE_HANDLE Heightmap;
    TEXTURE_HANDLE Weightmaps[4];
    uint EnabledChannels[4];

    vec2 HeightmapScaleBias;
    vec2 WeightmapScaleBias;

    // uint.MaxValue == no visibility layer on this tile
    uint VisibilityTextureIndex;
    uint VisibilityChannelIndex;
};

struct TileLayers
{
    int Layers[16]; // [weightmap * 4 + channel], an index into uLayerPalette plus one, 0 where no layer is painted
};

layout(std430, binding = BINDING_MATERIAL_DATA) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

layout(std430, binding = BINDING_LANDSCAPE_SCALES) restrict readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

layout(std430, binding = BINDING_LANDSCAPE_WEIGHT_MAPPING) restrict readonly buffer TileLayersBuffer
{
    TileLayers uTileLayers[];
};

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

in TE_OUT {
    vec3 vViewPos;
    mat3 TBN;
    float vWorldHeight;
    vec2 vTessCoord;
} fs_in;

#define COLOR_SLOPE 0u
#define COLOR_LAYERS 1u
#define COLOR_HEIGHT 2u
#define COLOR_TILES 3u

uniform float uSizeQuads;
uniform float uQuadCount;
uniform uint uColorMode;
uniform vec4 uLayerPalette[64]; // one color per layer name, shared by every tile
uniform int uIsolatedLayer; // index into uLayerPalette to show that layer
uniform float uSteepestSlope; // where the slope colors turn red
uniform vec2 uHeightRange;
uniform float uContourInterval;
uniform mat4 uViewMatrix;

// every painted layer in its own color, blended by weight
vec3 getColorFromLayers(PerMaterialData materialData, TileLayers layers)
{
    const vec3 unpainted = vec3(0.25);

    float quadFraction = 1.0 / uQuadCount;
    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;

    int weightmapCount = int(materialData.WeightmapCount);

    vec3 blendColor = vec3(0.0);
    float totalWeight = 0.0;
    float isolatedWeight = 0.0;

    for (int i = 0; i < weightmapCount; i++)
    {
        sampler2D weightmap = TO_SAMPLER(materialData.Weightmaps[i]);
        vec2 weightmapSize = textureSize(weightmap, 0);
        vec2 texelSize = 1.0 / weightmapSize;
        vec2 weightmapUvSize = vec2(uSizeQuads) / weightmapSize;

        vec2 uv2 = materialData.WeightmapScaleBias + subPatchOffset * weightmapUvSize + fs_in.vTessCoord * (weightmapUvSize * quadFraction);
        uv2 = uv2 * (1.0 - texelSize) + 0.5 * texelSize;

        vec4 weightmapColor = texture(weightmap, uv2);
        for (int c = 0; c < 4; c++)
        {
            int layer = layers.Layers[i * 4 + c] - 1;
            if (layer < 0) continue; // the hole layer or an unused channel

            float weight = weightmapColor[c];
            totalWeight += weight;
            blendColor += uLayerPalette[layer].rgb * weight;

            if (layer == uIsolatedLayer)
            {
                isolatedWeight = weight;
            }
        }
    }

    if (uIsolatedLayer >= 0) return mix(unpainted, uLayerPalette[uIsolatedLayer].rgb, isolatedWeight);
    return totalWeight > 0.001 ? blendColor / totalWeight : unpainted;
}

// flat ground green, yellow half way, red for the steepest slope
vec3 getColorFromSlope(vec3 viewNormal)
{
    float up = clamp(dot(viewNormal, normalize(uViewMatrix[1].xyz)), -1.0, 1.0);
    float slope = degrees(acos(up));

    vec3 color = mix(vec3(0.15, 0.6, 0.15), vec3(0.9, 0.8, 0.1), smoothstep(uSteepestSlope * 0.1, uSteepestSlope * 0.55, slope));
    return mix(color, vec3(0.85, 0.1, 0.05), smoothstep(uSteepestSlope * 0.55, uSteepestSlope, slope));
}

vec3 getColorFromId(uint id)
{
    id = id * 747796405u + 2891336453u;
    id = ((id >> ((id >> 28u) + 4u)) ^ id) * 277803737u;
    id = (id >> 22u) ^ id;

    float hue = float(id & 0xFFFFu) / 65535.0;
    vec3 rgb = clamp(abs(mod(hue * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return mix(vec3(1.0), rgb, 0.65) * 0.85;
}

// 1 on a line, 0 away from it
float contourLine(float height, float interval, float width)
{
    float h = height / interval;
    float change = fwidth(h);
    float distanceToLine = abs(fract(h - 0.5) - 0.5) / max(change, 1e-6);
    float line = 1.0 - clamp(distanceToLine - (width - 1.0), 0.0, 1.0);

    return line * smoothstep(1e-4, 5e-4, change);
}

// deep water at -0.25, shore at 0, snow at 1
vec3 getColorFromHeight(float height)
{
    vec3 color = vec3(0.0);
    if (height < -0.15)
    {
        float t = clamp((height + 0.25) / 0.1, 0.0, 1.0);
        color = mix(vec3(0.0, 0.02, 0.1), vec3(0.0, 0.1, 0.4), t);
    }
    else if (height < -0.05)
    {
        float t = (height + 0.15) / 0.1;
        color = mix(vec3(0.0, 0.1, 0.4), vec3(0.0, 0.4, 0.8), t);
    }
    else if (height < 0.0)
    {
        float t = (height + 0.05) / 0.05;
        color = mix(vec3(0.0, 0.4, 0.8), vec3(0.9, 0.85, 0.6), t);
    }
    else if (height < 0.1)
    {
        float t = height / 0.1;
        color = mix(vec3(0.9, 0.85, 0.6), vec3(0.6, 0.4, 0.2), t);
    }
    else if (height < 0.3)
    {
        float t = (height - 0.1) / 0.2;
        color = mix(vec3(0.6, 0.4, 0.2), vec3(0.1, 0.5, 0.1), t);
    }
    else if (height < 0.85)
    {
        float t = (height - 0.3) / 0.55;
        color = mix(vec3(0.1, 0.5, 0.1), vec3(0.3, 0.3, 0.3), t);
    }
    else
    {
        float t = clamp((height - 0.85) / 0.15, 0.0, 1.0);
        color = mix(vec3(0.3, 0.3, 0.3), vec3(1.0, 1.0, 1.0), t);
    }
    return color;
}

void main()
{
    PerDrawStatic draw = uDrawStatic[gDrawID];
    PerDrawCulled culled = FetchCulled(gDrawID);
    PerMaterialData material = uMaterialDataBuffer[draw.BaseMaterial + culled.MaterialIndex];

    vec3 normal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));

    vec3 color = vec3(0.25);
    if (uColorMode == COLOR_SLOPE)
    {
        color = getColorFromSlope(normal);
    }
    else if (uColorMode == COLOR_LAYERS && material.IsReady)
    {
        color = getColorFromLayers(material, uTileLayers[draw.BaseMaterial + culled.MaterialIndex]);
    }
    else if (uColorMode == COLOR_HEIGHT)
    {
        float t = clamp((fs_in.vWorldHeight - uHeightRange.x) / max(uHeightRange.y - uHeightRange.x, 0.001), 0.0, 1.0);
        color = getColorFromHeight(mix(-0.25, 1.0, t));
    }
    else if (uColorMode == COLOR_TILES)
    {
        color = getColorFromId(draw.PickingId);
    }

    color = pow(color, vec3(2.2));
    if (uContourInterval > 0.0)
    {
        // a thin line every interval, a thicker one every 5th
        float line = max(contourLine(fs_in.vWorldHeight, uContourInterval, 1.0), contourLine(fs_in.vWorldHeight, uContourInterval * 5.0, 1.5));
        color = mix(color, color * 0.15, line);
    }

    bool unlit = false;
    float opacity = 1.0;
    ApplyWire(color, unlit, opacity);

    gPosition = fs_in.vViewPos;
    gNormal = normal;
    gColor.rgb = color;
    gColor.a = unlit ? 0.0 : 1.0;
    gSpecular.rgb = vec3(0.5, 0.0, 1.0);
    gSpecular.a = 1.0; // free space
    gPicking = draw.PickingId;
}
