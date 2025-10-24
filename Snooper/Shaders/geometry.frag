#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;
layout (location = 4) out uint gPicking;

struct PerMaterialData
{
    bool IsReady;
    uint TextureFlags; // Bit 0: HasDiffuse, Bit 1: HasNormal, Bit 2: HasSpecular, Bit 3: IsTranslucent
    sampler2D Diffuse;
    sampler2D Normal;
    sampler2D Specular;
    vec2 Roughness;
    vec3 DiffuseColor;
};

layout(std430, binding = 2) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

uniform mat4 uViewMatrix;
uniform int uDebugColorMode;

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    vec4 vColor;
    vec2 vExtraTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

void main()
{
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];
    
    bool hasDiffuse = (materialData.TextureFlags & 1u) != 0u;
    bool hasNormal = (materialData.TextureFlags & 2u) != 0u;
    bool hasSpecular = (materialData.TextureFlags & 4u) != 0u;
    
    vec3 color = fs_in.vDebugColor;
    vec3 spec = vec3(1.0);
    if (uDebugColorMode == 0 && materialData.IsReady)
    {
        if (hasDiffuse)
        {
            color = texture(materialData.Diffuse, fs_in.vTexCoords).rgb * materialData.DiffuseColor;
        }
        
        if (hasSpecular)
        {
            spec = texture(materialData.Specular, fs_in.vTexCoords).rgb;
            spec.b = mix(materialData.Roughness.x, materialData.Roughness.y, spec.b);
        }
        else
        {
            spec = vec3(0.5, 0.5, materialData.Roughness.y);
        }
    }
    else if (uDebugColorMode == 4)
    {
        color = mix(vec3(0.25), vec3(1.0), vec3(
            float((gl_PrimitiveID * 61u) % 255u) / 255.0,
            float((gl_PrimitiveID * 149u) % 255u) / 255.0,
            float((gl_PrimitiveID * 233u) % 255u) / 255.0
        ));
    }
    else if (uDebugColorMode == 5)
    {
        color = fs_in.vColor.rgb;
    }
    
    vec3 normal = vec3(0.0, 0.0, 1.0);
    if (materialData.IsReady && hasNormal)
    {
        vec2 xy = texture(materialData.Normal, fs_in.vTexCoords).rg * 2.0 - 1.0;
        float z = sqrt(max(0.0, 1.0 - dot(xy, xy)));
        normal = normalize(vec3(xy, z));
    }

    gPosition = fs_in.vViewPos;
    gNormal = mat3(uViewMatrix) * normalize(fs_in.TBN * normal);
    gColor.rgb = color;
    gColor.a = 1.0; // free space
    gSpecular.rgb = spec.rgb;
    gSpecular.a = 1.0; // free space
    gPicking = cmd.PickingId;
}