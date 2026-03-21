#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

#include "material_sampling.glsl"

uniform int uFragmentColorMode;

#include "pbr.glsl"
#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

flat in uint vTexLayer;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vFragColor;
} fs_in;

out vec4 FragColor;

void main()
{
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];

    vec4 color = vec4(fs_in.vFragColor, 1.0);
    vec3 spec = vec3(1.0);
    vec3 normal = vec3(0.0, 0.0, 1.0);

    float opacity = 1.0;
    if (uFragmentColorMode == 0 && materialData.IsReady)
    {
        LayerData layerData = SampleLayer(materialData, vTexLayer, fs_in.vTexCoords);

        uint blendMode = GetBlendMode(materialData);
        if (blendMode == 1u && layerData.diffuse.a < 0.3333) // masked
        {
            discard;
        }
        else if (blendMode == 2u) // translucent
        {
            opacity = layerData.diffuse.a;
        }
        else if (blendMode == 3u) // additive
        {
            opacity = layerData.diffuse.r;
        }

        color = layerData.diffuse;
        spec = layerData.specular;
        normal = layerData.normal;
    }

    normal = normalize(fs_in.TBN * normal);
    if (uFragmentColorMode == 6) // Normals
    {
        color = vec4(normal, 1.0);
    }

    vec3 albedo = color.rgb;
    float metallic = spec.g;
    float roughness = spec.b;
    vec3 F0 = mix(vec3(0.04), albedo, metallic);
    vec3 V = normalize(-fs_in.vViewPos);

    vec3 skyColor = vec3(1.0);
    vec3 groundColor = vec3(0.5);
    float ndotUp = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 ambient = mix(groundColor, skyColor, ndotUp) * albedo;

    const int lightCount = 3;
    vec3 lightDirs[3] = vec3[3](
        normalize(vec3(0.5, 1.0, 0.3)),   // Key
        normalize(vec3(-0.3, 0.5, 0.8)),  // Fill
        normalize(vec3(0.0, -0.5, -1.0))  // Back
    );
    vec3 lightColors[3] = vec3[3](
        vec3(1.0, 0.8, 0.6),
        vec3(0.6, 0.8, 1.0),
        vec3(0.8, 0.8, 1.0)
    );
    float lightIntensity[3] = float[3](0.8, 0.6, 0.4);

    vec3 finalColor = EvaluatePBR(
        albedo,
        normal,
        V,
        metallic,
        roughness,
        F0,
        lightCount,
        lightDirs,
        lightColors,
        lightIntensity,
        ambient
    );

    finalColor = pow(finalColor, vec3(1.0 / 2.2));
    FragColor = vec4(finalColor, opacity);

    gPicking = cmd.PickingId;
}
