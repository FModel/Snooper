#extension GL_ARB_bindless_texture : require

layout (quads, fractional_odd_spacing, ccw) in;

struct PerInstanceData
{
    mat4 Matrix;
};

struct PerDrawData
{
    bool IsReady;
    uint LayerWeightCounts;

    sampler2D Heightmap;
    sampler2D Weightmaps[4];

    vec2 HeightmapScaleBias;
    vec2 WeightmapScaleBias;
    
    uint TextureIndex[16];
    uint ChannelIndex[16];
    uint Layer_Name[16 * 8];
    vec4 DebugColor[16];
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

layout(std430, binding = 1) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

layout(std430, binding = 2) restrict readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

in flat int tcInstanceIndex[];
in flat int tcDrawIndex[];

uniform float uSizeQuads;
uniform float uQuadCount;
uniform float uGlobalScale;
uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform uint uColorMode;
uniform uint uLayerName[8];

out TE_OUT {
    vec3 vViewPos;
    mat3 TBN;
    vec3 vColor;
} te_out;

bool comparePackedName(uint layerName[8], uint targetName[8])
{
    for (int i = 0; i < 8; i++)
    {
        if (layerName[i] != targetName[i])
        {
            return false;
        }
    }
    return true;
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

vec3 getColorFromWeightmap(PerDrawData drawData)
{
    float quadFraction = 1.0 / uQuadCount;
    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;
    
    int layerCount = int(drawData.LayerWeightCounts & 0xFFFFu);
    int weightmapCount = int((drawData.LayerWeightCounts >> 16) & 0xFFFFu);
    
    vec3 blendColor = vec3(0.0);
    float gray = 0.0;
    
    for (int i = 0; i < weightmapCount; ++i)
    {
        vec2 weightmapSize = textureSize(drawData.Weightmaps[i], 0);
        vec2 texelSize = 1.0 / weightmapSize;
        vec2 weightmapUvSize = vec2(uSizeQuads) / weightmapSize;

        vec2 uv2 = drawData.WeightmapScaleBias + subPatchOffset * weightmapUvSize + gl_TessCoord.xy * (weightmapUvSize * quadFraction);
        uv2 = uv2 * (1.0 - texelSize) + 0.5 * texelSize;
        
        vec4 weightmapColor = texture(drawData.Weightmaps[i], uv2);
        
        for (int j = 0; j < layerCount; ++j)
        {
            if (i != drawData.TextureIndex[j]) continue;
            
            float weight = weightmapColor[drawData.ChannelIndex[j]];
            if (weight <= 0.0) continue;

            uint layerPacked[8];
            for (int k = 0; k < 8; k++)
            {
                layerPacked[k] = drawData.Layer_Name[j * 8 + k];
            }

            if (comparePackedName(layerPacked, uLayerName))
            {
                float alpha = drawData.DebugColor[j].a;
                blendColor += drawData.DebugColor[j].rgb * weight * alpha;
            }
            else
            {
                gray += weight;
            }
        }
    }

    blendColor += vec3(gray) * 0.05;
    return blendColor;
}

void main()
{
    float u = gl_TessCoord.x;
    float v = gl_TessCoord.y;
    
    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;

    vec4 p0 = (p01 - p00) * u + p00;
    vec4 p1 = (p11 - p10) * u + p10;
    vec4 p = (p1 - p0) * v + p0;

    vec4 normal = normalize(vec4(cross((p10 - p00).xyz, (p01 - p00).xyz), 0));
    mat4 matrix = uViewMatrix * uInstanceDataBuffer[tcInstanceIndex[0]].Matrix;
    
    PerDrawData drawData = uDrawDataBuffer[tcDrawIndex[0]];
    if (!drawData.IsReady)
    {
        te_out.vViewPos = vec3(0.0);
        te_out.TBN = mat3(uViewMatrix);
        te_out.vColor = vec3(1.0);
        gl_Position = uProjectionMatrix * matrix * p;
        return;
    }
    
    vec2 heightmapSize = textureSize(drawData.Heightmap, 0);
    vec2 texelSize = 1.0 / heightmapSize;
    vec2 componentUvSize = vec2(uSizeQuads) / heightmapSize;
    float quadFraction = 1.0 / uQuadCount;

    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;
    vec2 uv = drawData.HeightmapScaleBias + subPatchOffset * componentUvSize + vec2(u, v) * (componentUvSize * quadFraction);
    uv = uv * (1.0 - texelSize) + 0.5 * texelSize;

    vec4 color = texture(drawData.Heightmap, uv);
    float R = color.r * 255.0;
    float G = color.g * 255.0;
    float height = ((R * 256.0) + G - 32768.0) / 128.0 * uGlobalScale;

    float nx = 2.0 * color.b - 1.0;
    float nz = 2.0 * color.a - 1.0;
    float ny = sqrt(1.0 - nx * nx + nz * nz);
    te_out.TBN = mat3(uViewMatrix) * mat3(normalize(vec3(-nz, 0.0, nx)), normalize(vec3(0.0, nz, -ny)), normalize(vec3(nx, ny, nz)));

    // displace point along normal
    p += normal * height;

    vec4 viewPos = matrix * p;
    gl_Position = uProjectionMatrix * viewPos;
    te_out.vViewPos = viewPos.xyz;
    
    if (uColorMode == 0)
    {
        te_out.vColor = getColorFromHeight(height);
    }
    else if (uColorMode == 1)
    {
        te_out.vColor = getColorFromWeightmap(drawData);
    }
    te_out.vColor = pow(te_out.vColor, vec3(2.2)); // to srgb
}