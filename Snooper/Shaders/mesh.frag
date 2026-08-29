#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

#include "pbr.glsl"
#include "Buffers/CommonMesh.frag"

out vec4 FragColor;

void main()
{
    PerDrawStatic draw = uDrawStatic[gDrawID];
    PerDrawCulled culled = FetchCulled(gDrawID);
    Surface surface = ResolveSurface(uMaterialDataBuffer[draw.BaseMaterial + culled.MaterialIndex]);

    if (surface.Discard)
    {
        discard;
    }

    vec3 albedo = surface.Color;
    vec3 normal = surface.Normal;
    float metallic = surface.Specular.g;
    float roughness = surface.Specular.b;
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
    FragColor = vec4(finalColor * surface.Opacity, surface.Additive ? 0.0 : surface.Opacity);

    gPicking = draw.PickingId;
}
