in vec2 vTexCoords;

uniform sampler2D gPosition; // view space position
uniform sampler2D gNormal; // view space normal
uniform sampler2D gColor; // albedo color (RGB: albedo, A: unused atm)
uniform sampler2D gSpecular; // specular color (R: unused atm, G: metallic, B: roughness, A: unused atm)
uniform sampler2D ssao;

uniform bool useSsao;
uniform int uLightCount;
uniform vec3 uLightDirs[3]; // in view space
uniform vec3 uLightColors[3];
uniform float uLightIntensity[3];

out vec4 FragColor;

#include "pbr.glsl"

void main()
{
    vec3 position = texture(gPosition, vTexCoords).rgb;
    vec3 normal = texture(gNormal, vTexCoords).rgb;
    vec3 albedo = texture(gColor, vTexCoords).rgb;
    vec3 specs = texture(gSpecular, vTexCoords).rgb;
    float ao = useSsao ? texture(ssao, vTexCoords).r : 1.0;

    // hemispheric lighting
    vec3 skyColor = vec3(1.0);
    vec3 groundColor = vec3(0.5);
    float ndotUp = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 ambient = mix(groundColor, skyColor, ndotUp) * albedo * ao;
    
    if (specs == vec3(0.0))
    {
        ambient = pow(ambient, vec3(1.0 / 2.2));
        FragColor = vec4(ambient, 1.0);
        return;
    }
    
    float whatever = specs.r;
    float metallic = specs.g;
    float roughness = specs.b;

    vec3 V = normalize(-position);
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    vec3 color = EvaluatePBR(albedo, normal, V, metallic, roughness, F0,
        uLightCount, uLightDirs, uLightColors, uLightIntensity,
        ambient);

    color = pow(color, vec3(1.0 / 2.2));
    FragColor = vec4(color, 1.0);
}