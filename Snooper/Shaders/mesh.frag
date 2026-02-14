#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

#include "material_sampling.glsl"

uniform int uDebugColorMode;

#include "pbr.glsl"
#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

flat in uint vTexLayer;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    vec4 vColor;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

out vec4 FragColor;

void main()
{
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];

    vec4 color = vec4(fs_in.vDebugColor, 1.0);
    vec3 spec = vec3(1.0);
    vec3 normal = vec3(0.0, 0.0, 1.0);

    if (uDebugColorMode == 0 && materialData.IsReady)
    {
        // Sample material for the current layer
        LayerData layerData = SampleLayer(materialData, vTexLayer, fs_in.vTexCoords);

        // Handle translucency
        if (IsTranslucent(materialData) && layerData.diffuse.a < 0.1)
        {
            discard;
        }

        color = layerData.diffuse;
        spec = layerData.specular;
        normal = layerData.normal;
    }
    else if (uDebugColorMode == 4)
    {
        color.rgb = mix(vec3(0.25), vec3(1.0), vec3(
            float((gl_PrimitiveID * 61u) % 255u) / 255.0,
            float((gl_PrimitiveID * 149u) % 255u) / 255.0,
            float((gl_PrimitiveID * 233u) % 255u) / 255.0
        ));
    }
    else if (uDebugColorMode == 5)
    {
        color = fs_in.vColor;
    }

    normal = normalize(fs_in.TBN * normal);

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
    FragColor = vec4(finalColor, 1.0);

    gPicking = cmd.PickingId;
}
