in vec2 vTexCoords;

uniform sampler2D gPosition; // view space position
uniform sampler2D gNormal; // view space normal
uniform sampler2D gColor; // albedo color (RGB: albedo, A: unused atm)
uniform sampler2D gSpecular; // specular color (R: unused atm, G: metallic, B: roughness, A: unused atm)
uniform sampler2D ssao;
uniform sampler2D shadowMap;

uniform bool useSsao;
uniform int uLightCount;
uniform vec3 uLightDirs[3]; // in view space
uniform vec3 uLightColors[3];
uniform float uLightIntensity[3];

uniform mat4 uInverseViewMatrix;
uniform mat4 uLightViewProjectionMatrix;
uniform vec3 uLightPos;

out vec4 FragColor;

#include "pbr.glsl"

float CalculateShadow(vec3 worldPos, vec3 worldNormal)
{
    // Transform world position to light space
    vec4 fragPosLightSpace = uLightViewProjectionMatrix * vec4(worldPos, 1.0);

    // Perform perspective divide
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;

    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

    // Check if we're outside the shadow map bounds - return 0 (no shadow)
    if(projCoords.z > 1.0)
        return 0.0;

    // Get depth of current fragment from light's perspective
    float currentDepth = projCoords.z;

//    vec3 normal = normalize(worldNormal);
//    vec3 lightDir = normalize(uLightPos - worldPos);
//    float bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.005);

    // PCF
    float shadow = 0.0;
    vec2 texelSize = 1.0 / textureSize(shadowMap, 0);
    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = texture(shadowMap, projCoords.xy + vec2(x, y) * texelSize).r;
            shadow += (currentDepth/* - bias*/) > pcfDepth ? 1.0 : 0.0;
        }
    }
    shadow /= 8.0;

    // Keep the shadow at 0.0 when outside the far_plane region of the light's frustum
    if(projCoords.z > 1.0)
        shadow = 0.0;

    return shadow;
}

void main()
{
    vec3 viewPos = texture(gPosition, vTexCoords).rgb;
    vec3 normal = texture(gNormal, vTexCoords).rgb;
    vec3 albedo = texture(gColor, vTexCoords).rgb;
    vec3 specs = texture(gSpecular, vTexCoords).rgb;
    float ao = useSsao ? texture(ssao, vTexCoords).r : 1.0;

    // Reconstruct world position from view position
    vec3 worldPos = (uInverseViewMatrix * vec4(viewPos, 1.0)).xyz;
    vec3 worldNormal = (uInverseViewMatrix * vec4(normal, 0.0)).xyz;
    // Calculate shadow (1.0 = in shadow, 0.0 = not in shadow)
    float shadow = CalculateShadow(worldPos, worldNormal);

    // Hemispheric lighting
    vec3 skyColor = vec3(1.0);
    vec3 groundColor = vec3(0.5);
    float ndotUp = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 ambient = mix(groundColor, skyColor, ndotUp) * albedo * ao;

    if (specs == vec3(0.0))
    {
        ambient *= (1.0 - shadow * 0.5);
        ambient = pow(ambient, vec3(1.0 / 2.2));
        FragColor = vec4(ambient, 1.0);
        return;
    }

    float whatever = specs.r;
    float metallic = specs.g;
    float roughness = specs.b;

    vec3 V = normalize(-viewPos);
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    vec3 directLight = EvaluatePBR(albedo, normal, V, metallic, roughness, F0,
        uLightCount, uLightDirs, uLightColors, uLightIntensity,
        vec3(0.0)); // Don't include ambient in PBR

    vec3 color = ambient + directLight * (1.0 - shadow);
    color = pow(color, vec3(1.0 / 2.2));
    FragColor = vec4(color, 1.0);
}
