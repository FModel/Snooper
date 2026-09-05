in vec2 vTexCoords;

uniform sampler2D gPosition; // view space position
uniform sampler2D gNormal; // view space normal
uniform sampler2D gColor; // albedo color (RGB: albedo, A: unused atm)
uniform sampler2D gSpecular; // specular color (R: unused atm, G: metallic, B: roughness, A: unused atm)
uniform sampler2D ssao;
uniform sampler2DArrayShadow shadowMap;

uniform bool useSsao;
uniform bool useShadows;
uniform bool useSunLight;
uniform bool useLighting;

uniform vec3 uSunDirection; // world space directional sun light
uniform vec3 uSunColor;
uniform float uSunIntensity;

uniform mat4 uInverseViewMatrix;

// Clustered lighting uniforms
uniform int uGridDimX;
uniform int uGridDimY;
uniform int uGridDimZ;
uniform float uZNear;
uniform float uZFar;

uniform int uShadowCascadeCount;
uniform float uShadowSoftness;
uniform float uShadowNormalOffset;
uniform float uShadowBlend;

out vec4 FragColor;

#include "pbr.glsl"
#include "Buffers/PerLightData.glsl"
#include "Buffers/ShadowViewData.glsl"

const int SHADOW_PCF_TAPS = 3;

float SampleShadowMap(ShadowViewData view, vec3 worldPos, vec3 worldNormal, float NdotL)
{
    float sinTheta = sqrt(max(0.0, 1.0 - NdotL * NdotL));
    float kernelWorld = view.texelWorldSize * (uShadowSoftness * float(SHADOW_PCF_TAPS - 1) + 1.0);
    vec3 samplePos = worldPos + worldNormal * (kernelWorld * sinTheta * uShadowNormalOffset);

    vec4 clip = view.viewProjection * vec4(samplePos, 1.0);
    vec3 coords = clip.xyz / clip.w * 0.5 + 0.5;

    if (coords.z >= 1.0)
        return 0.0; // past this view's far plane

    float tanTheta = min(sinTheta / max(NdotL, 1e-3), 8.0);
    float reference = coords.z - view.texelWorldSize * tanTheta * view.depthScale;

    vec2 texelStep = uShadowSoftness / vec2(textureSize(shadowMap, 0).xy);
    float layer = float(view.slot);

    float lit = 0.0;
    for (int y = -1; y <= 1; ++y)
    for (int x = -1; x <= 1; ++x)
    {
        vec2 uv = coords.xy + vec2(float(x), float(y)) * texelStep;
        lit += texture(shadowMap, vec4(uv, layer, reference));
    }

    return 1.0 - lit / float(SHADOW_PCF_TAPS * SHADOW_PCF_TAPS);
}

float CalculateSunShadow(vec3 worldPos, vec3 worldNormal, float NdotL, float viewDepth)
{
    int layer = -1;
    for (int i = 0; i < uShadowCascadeCount; ++i)
    {
        if (viewDepth < shadowViews[i].splitFar) { layer = i; break; }
    }

    if (layer < 0)
        return 0.0;

    float shadow = SampleShadowMap(shadowViews[layer], worldPos, worldNormal, NdotL);

    float splitNear = layer == 0 ? uZNear : shadowViews[layer - 1].splitFar;
    float splitFar = shadowViews[layer].splitFar;
    float bandStart = mix(splitFar, splitNear, uShadowBlend);
    float blend = clamp((viewDepth - bandStart) / max(splitFar - bandStart, 1e-4), 0.0, 1.0);

    if (blend > 0.0)
    {
        float next = layer + 1 < uShadowCascadeCount
            ? SampleShadowMap(shadowViews[layer + 1], worldPos, worldNormal, NdotL)
            : 0.0;
        shadow = mix(shadow, next, blend);
    }

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

float CalculateInverseSquareAttenuation(float distance, float range)
{
    // avoid singularity at distance = 0
    float invSq = 1.0 / max(1e-4, distance * distance);

    // smooth fade to zero near range to avoid popping (0..1)
    float fade = clamp(1.0 - pow(distance / range, 2.0), 0.0, 1.0);

    return invSq * fade;
}

const float LIGHT_CULL_THRESHOLD = 1e-4;

vec3 CalculatePointLight(PerLightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3 L = light.position - worldPos;
    float distance = length(L);

    if (distance > light.range)
        return vec3(0.0);

    L = L / distance;

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    float attenuation = light.UseInverseSquaredFalloff == 1
        ? CalculateInverseSquareAttenuation(distance, light.range)
        : CalculateAttenuation(distance, light.range);

    if (attenuation * NdotL * light.intensity < LIGHT_CULL_THRESHOLD)
        return vec3(0.0);

    vec3 H = normalize(worldV + L);
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

    return (diffuse + specular) * light.color * light.intensity * NdotL * attenuation;
}

vec3 CalculateSpotLight(PerLightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
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

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    // Smooth spot light falloff
    float epsilon = light.spotAngle - light.spotOuterAngle;
    float coneFalloff = clamp((theta - light.spotOuterAngle) / epsilon, 0.0, 1.0);

    float attenuation = light.UseInverseSquaredFalloff == 1
        ? CalculateInverseSquareAttenuation(distance, light.range)
        : CalculateAttenuation(distance, light.range);

    if (attenuation * coneFalloff * NdotL * light.intensity < LIGHT_CULL_THRESHOLD)
        return vec3(0.0);

    vec3 H = normalize(worldV + L);
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

    return (diffuse + specular) * light.color * light.intensity * NdotL * attenuation * coneFalloff;
}

// Calculate rectangular area light contribution
vec3 CalculateRectLight(PerLightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    // Direction from light center to shading point
    vec3 centerToPoint = worldPos - light.position;
    float distanceToCenter = length(centerToPoint);

    if (distanceToCenter > light.range)
        return vec3(0.0);

    // Build orthonormal basis for the rect light using the exact same axes as visualization
    // Forward = light direction (X axis in local space)
    vec3 forward = normalize(light.direction);

    // Check if point is behind the light (on the back side of the light plane)
    if (dot(centerToPoint, forward) < 0.0)
        return vec3(0.0);

    // Use the exact up vector from the light's rotation (Y axis in local space)
    vec3 heightDir = normalize(light.upVector);

    // Calculate width direction as cross product (Z axis in local space)
    vec3 widthDir = normalize(cross(forward, heightDir));

    // Now we have: forward = X, heightDir = Y, widthDir = Z
    // sizeX = width (Z direction), sizeY = height (Y direction)

    // Use Representative Point method (approximate but efficient)
    // Find the closest point on the rect light to the shading point
    float halfWidth = light.sizeX * 0.5;
    float halfHeight = light.sizeY * 0.5;

    // Project centerToPoint onto the rect's local axes
    float projWidth = dot(centerToPoint, widthDir);
    float projHeight = dot(centerToPoint, heightDir);

    // Clamp to rect bounds
    float u = clamp(projWidth / halfWidth, -1.0, 1.0);
    float v = clamp(projHeight / halfHeight, -1.0, 1.0);

    // Calculate closest point on rect surface
    vec3 closestPoint = light.position + u * halfWidth * widthDir + v * halfHeight * heightDir;
    vec3 L = closestPoint - worldPos;
    float distance = length(L);

    if (distance < 0.001)
        return vec3(0.0);

    L = L / distance;

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    // Check if light surface is facing the point
    float lightNdotL = dot(forward, -L);
    if (lightNdotL <= 0.0)
        return vec3(0.0);

    // Area light attenuation (solid angle * distance falloff). Cheap, so compute it before the BRDF
    // and bail on negligible contributions rather than paying full GGX/Smith/Fresnel.
    float area = light.sizeX * light.sizeY;
    float solidAngle = (area * lightNdotL) / (distance * distance + area);
    float attenuation = CalculateAttenuation(distance, light.range);
    float areaAttenuation = solidAngle * attenuation;

    if (areaAttenuation * NdotL * light.intensity < LIGHT_CULL_THRESHOLD)
        return vec3(0.0);

    vec3 H = normalize(worldV + L);
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

    return (diffuse + specular) * light.color * light.intensity * NdotL * areaAttenuation;
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

    vec3 L = normalize(uSunDirection);
    float NdotL = max(dot(worldNormal, L), 0.0);

    float shadow = 0.0;
    if (useShadows && NdotL > 0.0)
    {
        shadow = CalculateSunShadow(worldPos, worldNormal, NdotL, -viewPos.z);
    }

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
        vec3 sunContrib = vec3(0.0);
        if (useSunLight && NdotL > 0.0)
        {
            sunContrib = albedo * uSunColor * NdotL * uSunIntensity * (1.0 - shadow);
        }

        vec3 lighting = ambient + sunContrib;
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
    vec3 H = normalize(worldV + L);
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
    if (useLighting)
    {
        ClusterData cluster = clusterData[GetClusterIndex(viewPos)];
        for (uint i = 0; i < cluster.count; i++)
        {
            uint lightIndex = lightIndices[cluster.offset + i];
            PerLightData light = lights[lightIndex];

            if (light.type == 0) // Point light
            {
                localLighting += CalculatePointLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
            }
            else if (light.type == 1) // Spot light
            {
                localLighting += CalculateSpotLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
            }
            else if (light.type == 2) // Rect light
            {
                localLighting += CalculateRectLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0);
            }
        }
    }

    // Combine all lighting
    vec3 color = ambient + sunLight + localLighting;

    // Gamma correction
    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(color, 1.0);
}
