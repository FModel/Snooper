in vec2 vTexCoords;

uniform sampler2D gPosition; // view space position
uniform sampler2D gNormal; // view space normal
uniform sampler2D gColor; // albedo color (RGB: albedo, A: unused atm)
uniform sampler2D gSpecular; // specular color (R: unused atm, G: metallic, B: roughness, A: unused atm)
uniform sampler2D ssao;
uniform sampler2D shadowMap;

uniform bool useSsao;
uniform bool useShadows;
uniform bool useSunLight;

uniform vec3 uSunDirection; // world space directional sun light
uniform vec3 uSunColor;
uniform float uSunIntensity;

uniform mat4 uInverseViewMatrix;
uniform mat4 uLightViewProjectionMatrix;

// Clustered lighting uniforms
uniform int uGridDimX;
uniform int uGridDimY;
uniform int uGridDimZ;
uniform float uZNear;
uniform float uZFar;

out vec4 FragColor;

#include "pbr.glsl"

struct LightData
{
    vec3 position;
    float range;
    vec3 color;
    uint type; // 0 = point, 1 = spot
    vec3 direction;
    float spotAngle;
    float spotOuterAngle;
    float intensity;
    uint padding1;
    uint padding2;
};

struct ClusterData
{
    uint offset;
    uint count;
};

layout(std430, binding = 6) readonly buffer LightBuffer
{
    LightData lights[];
};

layout(std430, binding = 7) readonly buffer ClusterDataBuffer
{
    ClusterData clusterData[];
};

layout(std430, binding = 8) readonly buffer LightIndexList
{
    uint lightIndices[];
};

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

    // Angle-dependent bias to prevent both acne and peter panning
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

uint GetClusterIndex(vec3 viewPos)
{
    vec2 screenPos = gl_FragCoord.xy;
    uint clusterX = uint(floor(screenPos.x / 32.0));
    uint clusterY = uint(floor(screenPos.y / 32.0));

    // Clamp to grid bounds
    clusterX = min(clusterX, uint(uGridDimX - 1));
    clusterY = min(clusterY, uint(uGridDimY - 1));

    // Calculate Z slice using exponential distribution
    // viewPos.z is negative in view space (OpenGL convention)
    float viewZ = -viewPos.z; // Make positive for depth calculation

    // Clamp viewZ to valid range
    viewZ = clamp(viewZ, uZNear, uZFar);

    // Inverse of exponential distribution: depth = zNear * pow(zFar/zNear, slice/gridDimZ)
    // Solving for slice: slice = log(depth/zNear) / log(zFar/zNear) * gridDimZ
    float depthRatio = uZFar / uZNear;
    float clusterZFloat = log(viewZ / uZNear) / log(depthRatio) * float(uGridDimZ);
    uint clusterZ = uint(clamp(clusterZFloat, 0.0, float(uGridDimZ - 1)));

    return clusterZ * uint(uGridDimX) * uint(uGridDimY) + clusterY * uint(uGridDimX) + clusterX;
}

float CalculateAttenuation(float distance, float range)
{
    // Smooth attenuation that reaches zero at range
    float attenuation = max(0.0, 1.0 - (distance * distance) / (range * range));
    return attenuation * attenuation;
}

vec3 CalculatePointLight(LightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3 L = light.position - worldPos;
    float distance = length(L);

    if (distance > light.range)
        return vec3(0.0);

    L = L / distance;
    vec3 H = normalize(worldV + L);

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    float NdotV = max(dot(worldNormal, worldV), 0.001);

    // PBR calculations
    vec3 F = FresnelSchlick(max(dot(H, worldV), 0.0), F0);
    float D = DistributionGGX(worldNormal, H, roughness);
    float G = GeometrySmith(worldNormal, worldV, L, roughness);

    vec3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);

    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;

    vec3 diffuse = kD * albedo / PI;

    float attenuation = CalculateAttenuation(distance, light.range);

    return (diffuse + specular) * light.color * light.intensity * NdotL * attenuation;
}

vec3 CalculateSpotLight(LightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3 L = light.position - worldPos;
    float distance = length(L);

    if (distance > light.range)
        return vec3(0.0);

    L = L / distance;

    // Spot light cone calculation
    float theta = dot(L, normalize(-light.direction));

    if (theta < light.spotOuterAngle)
        return vec3(0.0);

    vec3 H = normalize(worldV + L);

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    float NdotV = max(dot(worldNormal, worldV), 0.001);

    // PBR calculations
    vec3 F = FresnelSchlick(max(dot(H, worldV), 0.0), F0);
    float D = DistributionGGX(worldNormal, H, roughness);
    float G = GeometrySmith(worldNormal, worldV, L, roughness);

    vec3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);

    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;

    vec3 diffuse = kD * albedo / PI;

    // Smooth spot light falloff
    float epsilon = light.spotAngle - light.spotOuterAngle;
    float intensity = clamp((theta - light.spotOuterAngle) / epsilon, 0.0, 1.0);

    float attenuation = CalculateAttenuation(distance, light.range);

    return (diffuse + specular) * light.color * light.intensity * NdotL * attenuation * intensity;
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

    // Night time: much darker ambient with moon-like blue tint
    if (!useSunLight)
    {
        skyColor = vec3(0.05, 0.08, 0.15);
        groundColor = vec3(0.02, 0.02, 0.03);
    }

    vec3 ambient = mix(groundColor, skyColor, ndotUp) * albedo * ao * 0.75;

    // For non-PBR materials (specs == 0), use simple lighting
    if (specs == vec3(0.0))
    {
        float diffuse = max(dot(worldNormal, sunDir), 0.0);
        vec3 lighting = ambient + albedo * uSunColor * diffuse * uSunIntensity * (1.0 - shadow * 0.8) * (useSunLight ? 1.0 : 0.0);
        FragColor = vec4(pow(lighting, vec3(1.0 / 2.2)), 1.0);
        return;
    }

    float metallic = specs.g;
    float roughness = specs.b;

    // View direction
    vec3 V = normalize(-viewPos);
    vec3 worldV = normalize((uInverseViewMatrix * vec4(V, 0.0)).xyz);

    // Fresnel reflectance at normal incidence
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    // Calculate sun light contribution using PBR
    vec3 L = sunDir;
    vec3 H = normalize(worldV + L);
    float NdotL = max(dot(worldNormal, L), 0.0);
    float NdotV = max(dot(worldNormal, worldV), 0.001);

    // Sun light
    vec3 sunLight = vec3(0.0);
    if (useSunLight && NdotL > 0.0 && shadow < 0.99)
    {
        vec3 F = FresnelSchlick(max(dot(H, worldV), 0.0), F0);
        float D = DistributionGGX(worldNormal, H, roughness);
        float G = GeometrySmith(worldNormal, worldV, L, roughness);

        vec3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);

        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metallic;

        vec3 diffuse = kD * albedo / PI;

        sunLight = (diffuse + specular) * uSunColor * NdotL * uSunIntensity * (1.0 - shadow);
    }

    // Clustered lighting
    vec3 localLighting = vec3(0.0);

    uint clusterIndex = GetClusterIndex(viewPos);
    ClusterData cluster = clusterData[clusterIndex];

    // Extract 3D cluster coordinates from linear index (needed for both debug modes)
    uint clusterZ = clusterIndex / (uint(uGridDimX) * uint(uGridDimY));
    uint temp = clusterIndex % (uint(uGridDimX) * uint(uGridDimY));
    uint clusterY = temp / uint(uGridDimX);
    uint clusterX = temp % uint(uGridDimX);

    // Debug: Visualize cluster assignment
    #ifdef DEBUG_CLUSTER_VISUALIZATION
    // Create a checkerboard pattern with cluster light count overlay
    bool checkerboard = ((clusterX + clusterY) % 2) == 0;
    vec3 baseColor = checkerboard ? vec3(0.2, 0.2, 0.3) : vec3(0.3, 0.2, 0.2);

    if (cluster.count > 0)
    {
        // Color based on number of lights in cluster
        float intensity = float(cluster.count) / 7.0; // Normalize by expected max lights
        baseColor = mix(baseColor, vec3(0.0, 1.0, 0.0), intensity);
    }

    FragColor = vec4(baseColor, 1.0);
    return;
    #endif

    // Debug: Test if we have any lights at all by checking all lights directly
    // This bypasses clustering to see if the light data is valid
    #ifdef DEBUG_LIGHTS_NO_CLUSTERING
    for (uint i = 0; i < min(uint(10), uint(lights.length())); i++)
    {
        LightData light = lights[i];

        if (light.type == 0) // Point light
        {
            localLighting += CalculatePointLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
        }
        else if (light.type == 1) // Spot light
        {
            localLighting += CalculateSpotLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
        }
    }
    #else
    for (uint i = 0; i < cluster.count; i++)
    {
        uint lightIndex = lightIndices[cluster.offset + i];
        LightData light = lights[lightIndex];

        if (light.type == 0) // Point light
        {
            localLighting += CalculatePointLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
        }
        else if (light.type == 1) // Spot light
        {
            localLighting += CalculateSpotLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
        }
    }
    #endif

    // Combine all lighting
    vec3 color = ambient + sunLight + localLighting;

    // Gamma correction
    color = pow(color, vec3(1.0 / 2.2));

    // Debug: Overlay cluster grid visualization
    #ifdef DEBUG_CLUSTER_GRID_OVERLAY
    // Draw grid lines at cluster boundaries
    vec2 screenPos = gl_FragCoord.xy;
    vec2 clusterPos = vec2(clusterX, clusterY) * 32.0;
    vec2 posInCluster = screenPos - clusterPos;

    // Check if we're near a cluster boundary (within 1 pixel)
    bool onGridLine = (posInCluster.x < 1.0 || posInCluster.x > 31.0 ||
                       posInCluster.y < 1.0 || posInCluster.y > 31.0);

    if (onGridLine)
    {
        // Black grid lines
        color = vec3(0.0);
    }
    else if (cluster.count > 0)
    {
        // For clusters with lights assigned, show a distinct color
        // Different colors based on number of lights in the cluster
        vec3 clusterColor;
        if (cluster.count == 1)
        {
            clusterColor = vec3(0.0, 0.6, 1.0); // Blue for 1 light
        }
        else if (cluster.count == 2)
        {
            clusterColor = vec3(0.0, 1.0, 0.8); // Cyan for 2 lights
        }
        else if (cluster.count == 3)
        {
            clusterColor = vec3(0.0, 1.0, 0.3); // Green for 3 lights
        }
        else if (cluster.count == 4)
        {
            clusterColor = vec3(1.0, 1.0, 0.0); // Yellow for 4 lights
        }
        else if (cluster.count >= 5)
        {
            clusterColor = vec3(1.0, 0.5, 0.0); // Orange for 5+ lights
        }

        color = mix(color, clusterColor, 0.8);
    }
    #endif

    FragColor = vec4(color, 1.0);
}
