#extension GL_ARB_bindless_texture : require

struct PerDrawData
{
    bool IsReady;
    uint IsTranslucent;
    sampler2D Diffuse;
    sampler2D Normal;
    sampler2D Specular;
    vec2 Roughness;
};

layout(std430, binding = 1) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

uniform int uDebugColorMode;

#include "pbr.glsl"

in flat int mIndex;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

out vec4 FragColor;

void main()
{
    PerDrawData drawData = uDrawDataBuffer[mIndex];

    vec4 color = vec4(fs_in.vDebugColor, 1.0);
    vec3 spec = vec3(1.0);
    if (uDebugColorMode == 0 && drawData.IsReady)
    {
        color = texture(drawData.Diffuse, fs_in.vTexCoords);
        if (drawData.IsTranslucent == 1 && color.a < 0.1)
        {
            discard;
        }
        spec = texture(drawData.Specular, fs_in.vTexCoords).rgb;
        
        spec.b = mix(drawData.Roughness.x, drawData.Roughness.y, spec.b);
    }
    
    vec3 normal = vec3(0.0, 0.0, 1.0);
    if (drawData.IsReady)
    {
        vec2 xy = texture(drawData.Normal, fs_in.vTexCoords).rg * 2.0 - 1.0;
        float z = sqrt(max(0.0, 1.0 - dot(xy, xy)));
        normal = normalize(vec3(xy, z));
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
}