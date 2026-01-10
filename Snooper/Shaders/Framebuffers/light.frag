in vec2 vTexCoords;

uniform sampler2D gPosition; // view space position
uniform sampler2D gNormal; // view space normal
uniform sampler2D gColor; // albedo color (RGB: albedo, A: unused atm)
uniform sampler2D gSpecular; // specular color (R: unused atm, G: metallic, B: roughness, A: unused atm)
uniform sampler2D ssao;
uniform sampler2D shadowMap;

uniform bool useSsao;
uniform bool useShadows;
uniform vec3 uSunDirection; // world space directional sun light
uniform vec3 uSunColor;
uniform float uSunIntensity;

uniform mat4 uInverseViewMatrix;
uniform mat4 uLightViewProjectionMatrix;

out vec4 FragColor;

#include "pbr.glsl"

float CalculateShadow(vec3 worldPos, vec3 worldNormal, vec3 lightDir)
{
    // Transform world position to light space
    vec4 fragPosLightSpace = uLightViewProjectionMatrix * vec4(worldPos, 1.0);

    // Perform perspective divide
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;

    // Transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;

    // Check if we're outside the shadow map bounds - return 0 (no shadow)
    if(projCoords.z > 1.0 || projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0)
        return 0.0;

    // Get depth of current fragment from light's perspective
    float currentDepth = projCoords.z;

    // Angle-dependent bias to prevent both acne and peter panning (TODO: not perfect yet)
    vec3 normal = normalize(worldNormal);
    float NdotL = max(dot(normal, lightDir), 0.0);
    float bias = 0.000001 + 0.000003 * (1.0 - NdotL);

    // PCF (Percentage Closer Filtering) for softer shadow edges
    float shadow = 0.0;
    vec2 texelSize = 1.0 / textureSize(shadowMap, 0);
    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = texture(shadowMap, projCoords.xy + vec2(x, y) * texelSize).r;
            shadow += (currentDepth - bias) > pcfDepth ? 1.0 : 0.0;
        }
    }
    shadow /= 9.0;

    return shadow;
}

void main()
{
    vec3 viewPos = texture(gPosition, vTexCoords).rgb;
    vec3 normal = texture(gNormal, vTexCoords).rgb;
    vec3 albedo = texture(gColor, vTexCoords).rgb;
    vec3 specs = texture(gSpecular, vTexCoords).rgb;
    float ao = useSsao ? texture(ssao, vTexCoords).r : 1.0;

    // Reconstruct world position and normal from view space
    vec3 worldPos = (uInverseViewMatrix * vec4(viewPos, 1.0)).xyz;
    vec3 worldNormal = normalize((uInverseViewMatrix * vec4(normal, 0.0)).xyz);

    // Calculate shadow (1.0 = fully in shadow, 0.0 = not in shadow)
    vec3 sunDir = normalize(uSunDirection);
    float shadow = useShadows ? CalculateShadow(worldPos, worldNormal, sunDir) : 0.0;

    // Ambient lighting
    vec3 skyColor = vec3(0.6, 0.7, 0.8);
    vec3 groundColor = vec3(0.4, 0.35, 0.3);
    float ndotUp = clamp(worldNormal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 ambient = mix(groundColor, skyColor, ndotUp) * albedo * ao * 0.75;

    // For non-PBR materials (specs == 0), use simple lighting
    if (specs == vec3(0.0))
    {
        float diffuse = max(dot(worldNormal, sunDir), 0.0);
        vec3 lighting = ambient + albedo * uSunColor * diffuse * uSunIntensity * (1.0 - shadow * 0.8);
        lighting = pow(lighting, vec3(1.0 / 2.2));
        FragColor = vec4(lighting, 1.0);
        return;
    }

    float metallic = specs.g;
    float roughness = specs.b;

    // View direction in view space
    vec3 V = normalize(-viewPos);
    vec3 worldV = normalize((uInverseViewMatrix * vec4(V, 0.0)).xyz);

    // Fresnel reflectance at normal incidence
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    // Calculate sun light contribution using PBR
    vec3 L = sunDir;
    vec3 H = normalize(worldV + L);
    float NdotL = max(dot(worldNormal, L), 0.0);
    float NdotV = max(dot(worldNormal, worldV), 0.001);

    // Early out if surface is facing away from light or fully in shadow
    vec3 sunLight = vec3(0.0);
    if (NdotL > 0.0 && shadow < 0.99)
    {
        vec3 F = FresnelSchlick(max(dot(H, worldV), 0.0), F0);
        float D = DistributionGGX(worldNormal, H, roughness);
        float G = GeometrySmith(worldNormal, worldV, L, roughness);

        vec3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);

        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metallic;

        vec3 diffuse = kD * albedo / PI;

        // Final sun light contribution with shadow applied to everything
        sunLight = (diffuse + specular) * uSunColor * NdotL * uSunIntensity * (1.0 - shadow);
    }

    // Combine ambient and direct lighting
    vec3 color = ambient + sunLight;

    // Gamma correction
    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(color, 1.0);
}
