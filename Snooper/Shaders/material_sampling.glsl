// Material sampling utilities for multi-layer materials
// Shared between geometry.frag and mesh.frag

struct PerMaterialData
{
    bool IsReady;
    uint LayerCount;
    uint GlobalFlags;
    uint LayerTextureFlags;

    // Fixed arrays for up to 4 layers
    sampler2D Diffuse[4];
    sampler2D Normal[4];
    sampler2D Specular[4];

    // Per-layer material properties
    // Roughness: 2 floats per layer (min, max) * 4 layers = 8 floats
    // DiffuseColor: 3 floats per layer (RGB) * 4 layers = 12 floats
    float Roughness[8];
    float DiffuseColor[12];
};

layout(std430, binding = 2) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

// Check if a specific layer has a specific texture type
bool HasLayerTexture(PerMaterialData materialData, uint layer, uint textureType)
{
    // textureType: 0 = Diffuse, 1 = Normal, 2 = Specular
    uint layerFlags = (materialData.LayerTextureFlags >> (layer * 3u)) & 7u;
    return (layerFlags & (1u << textureType)) != 0u;
}

// Get roughness for a specific layer
vec2 GetLayerRoughness(PerMaterialData materialData, uint layer)
{
    return vec2(materialData.Roughness[layer * 2u], materialData.Roughness[layer * 2u + 1u]);
}

// Get diffuse color for a specific layer
vec3 GetLayerDiffuseColor(PerMaterialData materialData, uint layer)
{
    uint baseIndex = layer * 3u;
    return vec3(materialData.DiffuseColor[baseIndex], materialData.DiffuseColor[baseIndex + 1u], materialData.DiffuseColor[baseIndex + 2u]);
}

// Sample diffuse texture for a specific layer
vec4 SampleLayerDiffuse(PerMaterialData materialData, uint layer, vec2 uv)
{
    if (layer >= materialData.LayerCount)
        layer = 0u;

    if (HasLayerTexture(materialData, layer, 0u))
    {
        return texture(materialData.Diffuse[layer], uv);
    }

    return vec4(1.0);
}

// Sample normal texture for a specific layer and return tangent-space normal
vec3 SampleLayerNormal(PerMaterialData materialData, uint layer, vec2 uv)
{
    if (layer >= materialData.LayerCount)
        layer = 0u;

    if (HasLayerTexture(materialData, layer, 1u))
    {
        vec2 xy = texture(materialData.Normal[layer], uv).rg * 2.0 - 1.0;
        float z = sqrt(max(0.0, 1.0 - dot(xy, xy)));
        return normalize(vec3(xy, z));
    }

    return vec3(0.0, 0.0, 1.0);
}

// Sample specular texture for a specific layer
vec3 SampleLayerSpecular(PerMaterialData materialData, uint layer, vec2 uv)
{
    if (layer >= materialData.LayerCount)
        layer = 0u;

    if (HasLayerTexture(materialData, layer, 2u))
    {
        vec3 spec = texture(materialData.Specular[layer], uv).rgb;
        vec2 roughness = GetLayerRoughness(materialData, layer);
        spec.b = mix(roughness.x, roughness.y, spec.b);
        return spec;
    }

    // Default specular values if no texture
    vec2 roughness = GetLayerRoughness(materialData, layer);
    return vec3(0.5, 0.5, roughness.y);
}

// Sample all material properties for a layer
struct LayerData
{
    vec4 diffuse;
    vec3 normal;
    vec3 specular;
};

LayerData SampleLayer(PerMaterialData materialData, uint layer, vec2 uv)
{
    LayerData result;

    // Clamp layer to valid range
    if (layer >= materialData.LayerCount)
        layer = 0u;

    // Sample diffuse
    result.diffuse = SampleLayerDiffuse(materialData, layer, uv);
    result.diffuse.rgb *= GetLayerDiffuseColor(materialData, layer);

    // Sample normal
    result.normal = SampleLayerNormal(materialData, layer, uv);

    // Sample specular
    result.specular = SampleLayerSpecular(materialData, layer, uv);

    return result;
}

// Check global flags
bool IsTranslucent(PerMaterialData materialData)
{
    return (materialData.GlobalFlags & 1u) != 0u;
}
