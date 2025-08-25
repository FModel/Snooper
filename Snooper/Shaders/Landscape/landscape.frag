#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;

struct PerDrawData
{
    bool IsReady;
    uint WeightmapCount;

    sampler2D Heightmap;
    sampler2D Weightmaps[4];
    uint EnabledChannels[4];

    vec2 HeightmapScaleBias;
    vec2 WeightmapScaleBias;
};

struct WeightHighlightMapping
{
    uint WeightmapIndex;
    uint ChannelIndex;
    vec4 DebugColor;
};

layout(std430, binding = 1) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

layout(std430, binding = 2) restrict readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

layout(std430, binding = 3) restrict readonly buffer WeightMappingBuffer
{
    WeightHighlightMapping uWeightMappingBuffer[];
};

in flat int vDrawID;
in TE_OUT {
    vec3 vViewPos;
    mat3 TBN;
    float vHeight;
    vec2 vTessCoord;
} fs_in;

uniform float uSizeQuads;
uniform float uQuadCount;
uniform uint uColorMode;

bool channelEnabled(uint mask, int channel)
{
    return ((mask >> channel) & 1u) != 0u;
}

vec3 getColorFromWeightmap(PerDrawData drawData, WeightHighlightMapping mapping)
{
    float quadFraction = 1.0 / uQuadCount;
    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;

    int weightmapCount = int(drawData.WeightmapCount);

    vec3 blendColor = vec3(0.0);
    float gray = 0.0;

    for (int i = 0; i < weightmapCount; i++)
    {
        vec2 weightmapSize = textureSize(drawData.Weightmaps[i], 0);
        vec2 texelSize = 1.0 / weightmapSize;
        vec2 weightmapUvSize = vec2(uSizeQuads) / weightmapSize;

        vec2 uv2 = drawData.WeightmapScaleBias + subPatchOffset * weightmapUvSize + fs_in.vTessCoord * (weightmapUvSize * quadFraction);
        uv2 = uv2 * (1.0 - texelSize) + 0.5 * texelSize;

        uint mask = drawData.EnabledChannels[i];
        vec4 weightmapColor = texture(drawData.Weightmaps[i], uv2);
        for (int c = 0; c < 4; c++)
        {
            if (!channelEnabled(mask, c))
            {
                continue;
            }

            float weight = weightmapColor[c];
            gray += weight;

            if (i == mapping.WeightmapIndex && c == mapping.ChannelIndex)
            {
                blendColor += mapping.DebugColor.rgb * weight * mapping.DebugColor.a;
            }
        }
    }

    blendColor += vec3(gray) * 0.1;
    return blendColor;
}

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
    vec3 color = vec3(1.0);
    if (uColorMode == 0)
    {
        color = getColorFromHeight(fs_in.vHeight);
    }
    else if (uColorMode == 1)
    {
        color = getColorFromWeightmap(uDrawDataBuffer[vDrawID], uWeightMappingBuffer[vDrawID]);
    }
    
    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    gColor.rgb = pow(color, vec3(2.2));
    gColor.a = 1.0; // free space
    gSpecular.rgb = vec3(0.0, 0.0, 0.0);
    gSpecular.a = 1.0; // free space
}