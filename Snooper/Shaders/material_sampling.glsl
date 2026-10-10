// Material sampling utilities for multi-layer materials
// Shared between geometry.frag and mesh.frag

#include "Buffers/bindless.glsl"

struct PerMaterialData
{
    bool IsReady;
    uint LayerCount;
    uint GlobalFlags;
    uint LayerTextureFlags;

    // Fixed arrays for up to 4 layers
    TEXTURE_HANDLE Diffuse[4];
    TEXTURE_HANDLE Normal[4];
    TEXTURE_HANDLE Specular[4];

    // Per-layer material properties
    // Roughness: 2 floats per layer (min, max) * 4 layers = 8 floats
    // DiffuseColor: 3 floats per layer (RGB) * 4 layers = 12 floats
    float Roughness[8];
    float DiffuseColor[12];

    float Opacity;
    uint Scalars;
};

layout(std430, binding = BINDING_MATERIAL_DATA) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

// every function takes the material's index and reads the fields it needs from the buffer: passing the struct
// by value copies twelve texture handles and twenty floats into registers per fragment, which costs occupancy

// Check if a specific layer has a specific texture type
bool HasLayerTexture(uint material, uint layer, uint textureType)
{
    // textureType: 0 = Diffuse, 1 = Normal, 2 = Specular
    uint layerFlags = (uMaterialDataBuffer[material].LayerTextureFlags >> (layer * 3u)) & 7u;
    return (layerFlags & (1u << textureType)) != 0u;
}

// Get roughness for a specific layer
vec2 GetLayerRoughness(uint material, uint layer)
{
    return vec2(uMaterialDataBuffer[material].Roughness[layer * 2u], uMaterialDataBuffer[material].Roughness[layer * 2u + 1u]);
}

// Get diffuse color for a specific layer
vec3 GetLayerDiffuseColor(uint material, uint layer)
{
    uint baseIndex = layer * 3u;
    return vec3(uMaterialDataBuffer[material].DiffuseColor[baseIndex], uMaterialDataBuffer[material].DiffuseColor[baseIndex + 1u], uMaterialDataBuffer[material].DiffuseColor[baseIndex + 2u]);
}

// Sample diffuse texture for a specific layer
vec4 SampleLayerDiffuse(uint material, uint layer, vec2 uv)
{
    if (layer >= uMaterialDataBuffer[material].LayerCount)
        layer = 0u;

    if (HasLayerTexture(material, layer, 0u))
    {
        return texture(TO_SAMPLER(uMaterialDataBuffer[material].Diffuse[layer]), uv);
    }

    return vec4(1.0);
}

// Sample normal texture for a specific layer and return tangent-space normal
vec3 SampleLayerNormal(uint material, uint layer, vec2 uv)
{
    if (layer >= uMaterialDataBuffer[material].LayerCount)
        layer = 0u;

    if (HasLayerTexture(material, layer, 1u))
    {
        vec2 xy = texture(TO_SAMPLER(uMaterialDataBuffer[material].Normal[layer]), uv).rg * 2.0 - 1.0;
        float z = sqrt(max(0.0, 1.0 - dot(xy, xy)));
        return normalize(vec3(xy, z));
    }

    return vec3(0.0, 0.0, 1.0);
}

// Sample specular texture for a specific layer
vec3 SampleLayerSpecular(uint material, uint layer, vec2 uv)
{
    if (layer >= uMaterialDataBuffer[material].LayerCount)
        layer = 0u;

    if (HasLayerTexture(material, layer, 2u))
    {
        vec3 spec = texture(TO_SAMPLER(uMaterialDataBuffer[material].Specular[layer]), uv).rgb;
        vec2 roughness = GetLayerRoughness(material, layer);
        spec.b = mix(roughness.x, roughness.y, spec.b);

        // LayerTextureFlags bits 12 and up, 3 per layer: the channels this texture does not have (specular, metallic,
        // roughness), the material's own values stand in for those
        uint missing = (uMaterialDataBuffer[material].LayerTextureFlags >> (12u + layer * 3u)) & 7u;
        if (missing != 0u)
        {
            vec2 scalars = unpackUnorm4x8(uMaterialDataBuffer[material].Scalars).xy;
            if ((missing & 1u) != 0u) spec.r = scalars.x;
            if ((missing & 2u) != 0u) spec.g = scalars.y;
            if ((missing & 4u) != 0u) spec.b = roughness.y;
        }

        return spec;
    }

    vec2 roughness = GetLayerRoughness(material, layer);
    return vec3(unpackUnorm4x8(uMaterialDataBuffer[material].Scalars).xy, roughness.y);
}

// Sample all material properties for a layer
struct LayerData
{
    vec4 diffuse;
    vec3 normal;
    vec3 specular;
};

LayerData SampleLayer(uint material, uint layer, vec2 uv)
{
    LayerData result;

    // Clamp layer to valid range
    if (layer >= uMaterialDataBuffer[material].LayerCount)
        layer = 0u;

    // Sample diffuse
    result.diffuse = SampleLayerDiffuse(material, layer, uv);
    result.diffuse.rgb *= GetLayerDiffuseColor(material, layer);

    // Sample normal
    result.normal = SampleLayerNormal(material, layer, uv);

    // Sample specular
    result.specular = SampleLayerSpecular(material, layer, uv);

    return result;
}

// GlobalFlags: bits 0-3 are the blend mode, bit 4 says the material is unlit
uint GetBlendMode(uint material)
{
    return uMaterialDataBuffer[material].GlobalFlags & 0xFu;
}

bool IsUnlit(uint material)
{
    return (uMaterialDataBuffer[material].GlobalFlags & 0x10u) != 0u;
}
