// how a surface is lit, shared by the deferred lighting pass and the translucent meshes of the forward pass:
// both give it a surface, it gives back the same colour. Nothing is set per shader: what it reads is bound for the whole frame

#include "Lighting/scene_data.glsl"

layout(binding = UNIT_SHADOW_MAP) uniform sampler2DArrayShadow shadowMap;

#include "pbr.glsl"
#include "Buffers/ShadowViewData.glsl"

const int SHADOW_PCF_TAPS = 3;

float SampleShadowMap(ShadowViewData view, vec3 worldPos, vec3 worldNormal, float NdotL)
{
    float sinTheta = sqrt(max(0.0, 1.0 - NdotL * NdotL));
    float kernelWorld = view.texelWorldSize * (uShadowSoftness * float(SHADOW_PCF_TAPS - 1) + 1.0);
    vec3 samplePos = worldPos + worldNormal * (kernelWorld * sinTheta * uShadowNormalOffset);

    vec4 clip = view.viewProjection * vec4(samplePos, 1.0);
    vec3 ndc = clip.xyz / clip.w;
    vec3 coords = vec3(ndc.xy * 0.5 + 0.5, ndc.z); // ZERO_TO_ONE clip control: depth is already [0, 1]

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

// the scene lights, left out on a device that cannot bind their buffers next to a mesh's
#ifdef CLUSTERED_LIGHTS
#include "Buffers/PerLightData.glsl"

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

float CalculateAttenuation(float distance, float range, float falloffExponent)
{
    float window = max(0.0, 1.0 - (distance * distance) / (range * range));
    return pow(window, falloffExponent);
}

vec3 NearestOnSegment(vec3 L0, vec3 L1)
{
    vec3 Ld = L1 - L0;
    float dd = dot(Ld, Ld);
    float t = dd > 0.0 ? clamp(-dot(L0, Ld) / dd, 0.0, 1.0) : 0.0;
    return L0 + Ld * t;
}

float CalculateInverseSquareAttenuation(float distance, float centerDistance, float range)
{
    // avoid singularity at distance = 0
    float invSq = 1.0 / max(1e-4, distance * distance);

    // smooth fade to zero near range to avoid popping (0..1)
    float fade = clamp(1.0 - pow(centerDistance / range, 2.0), 0.0, 1.0);

    return invSq * fade;
}

const float LIGHT_CULL_THRESHOLD = 1e-4;

vec3 AreaLightSpecular(vec3 L0, vec3 L1, float sourceRadius, float sourceLength, vec3 N, vec3 V, float NdotV, float roughness, vec3 F0)
{
    vec3 R = reflect(-V, N);

    vec3 Ld = L1 - L0;
    float RoLd = dot(R, Ld);
    float denom = dot(Ld, Ld) - RoLd * RoLd;
    float t = denom > 1e-6 ? clamp((dot(R, L0) * RoLd - dot(L0, Ld)) / denom, 0.0, 1.0) : 0.0;
    vec3 toLine = L0 + Ld * t;

    vec3 centerToRay = dot(toLine, R) * R - toLine;
    vec3 closest = toLine + centerToRay * clamp(sourceRadius / max(length(centerToRay), 1e-4), 0.0, 1.0);
    float distance = length(closest);
    vec3 L = closest / distance;
    float NdotL = max(dot(N, L), 0.0);
    vec3 H = normalize(V + L);

    // the engine's energy normalization: one dimension for the line, two for the sphere
    float alpha = roughness * roughness;
    float line = alpha / clamp(alpha + 0.5 * clamp(sourceLength / distance, 0.0, 1.0), 0.0, 1.0);
    float sphere = alpha / clamp(alpha + 0.5 * clamp(sourceRadius / distance, 0.0, 1.0), 0.0, 1.0);
    float energy = line * sphere * sphere;

    vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);
    float D = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);

    return (D * G * F) / (4.0 * NdotV * NdotL + 0.001) * NdotL * energy;
}

vec3 CalculatePointLight(PerLightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3 toCenter = light.position - worldPos;
    float centerDistance = length(toCenter);
    if (centerDistance > light.range)
        return vec3(0.0);

    vec3 halfTube = light.tangent * (0.5 * light.sourceLength);
    vec3 L0 = toCenter - halfTube;
    vec3 L1 = toCenter + halfTube;
    vec3 toLight = NearestOnSegment(L0, L1);
    float distance = length(toLight);
    vec3 L = toLight / distance;

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    float attenuation = (light.flags & LIGHT_INVERSE_SQUARED_FALLOFF) != 0u
        ? CalculateInverseSquareAttenuation(distance, centerDistance, light.range)
        : CalculateAttenuation(centerDistance, light.range, light.falloffExponent);

    if (attenuation * NdotL * light.intensity < LIGHT_CULL_THRESHOLD)
        return vec3(0.0);

    vec3 H = normalize(worldV + L);
    float NdotV = max(dot(worldNormal, worldV), 0.001);

    vec3 kD = (vec3(1.0) - FresnelSchlick(max(dot(H, worldV), 0.0), F0)) * (1.0 - metallic);
    vec3 diffuse = kD * albedo / PI * NdotL;
    vec3 specular = AreaLightSpecular(L0, L1, light.sourceRadius, light.sourceLength, worldNormal, worldV, NdotV, roughness, F0);

    return (diffuse + specular) * light.color * light.intensity * attenuation;
}

vec3 CalculateSpotLight(PerLightData light, vec3 worldPos, vec3 worldNormal, vec3 worldV, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3 toCenter = light.position - worldPos;
    float centerDistance = length(toCenter);
    if (centerDistance > light.range)
        return vec3(0.0);

    vec3 halfTube = light.tangent * (0.5 * light.sourceLength);
    vec3 L0 = toCenter - halfTube;
    vec3 L1 = toCenter + halfTube;
    vec3 toLight = NearestOnSegment(L0, L1);
    float distance = length(toLight);
    vec3 L = toLight / distance;

    // Spot light cone calculation
    float theta = dot(L, normalize(-light.direction));

    if (theta < light.spotOuterAngle)
        return vec3(0.0);

    float NdotL = max(dot(worldNormal, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    // the engine squares the blend between the two cones
    float epsilon = light.spotAngle - light.spotOuterAngle;
    float coneFalloff = clamp((theta - light.spotOuterAngle) / epsilon, 0.0, 1.0);
    coneFalloff *= coneFalloff;

    float attenuation = (light.flags & LIGHT_INVERSE_SQUARED_FALLOFF) != 0u
        ? CalculateInverseSquareAttenuation(distance, centerDistance, light.range)
        : CalculateAttenuation(centerDistance, light.range, light.falloffExponent);

    if (attenuation * coneFalloff * NdotL * light.intensity < LIGHT_CULL_THRESHOLD)
        return vec3(0.0);

    vec3 H = normalize(worldV + L);
    float NdotV = max(dot(worldNormal, worldV), 0.001);

    vec3 kD = (vec3(1.0) - FresnelSchlick(max(dot(H, worldV), 0.0), F0)) * (1.0 - metallic);
    vec3 diffuse = kD * albedo / PI * NdotL;
    vec3 specular = AreaLightSpecular(L0, L1, light.sourceRadius, light.sourceLength, worldNormal, worldV, NdotV, roughness, F0);

    return (diffuse + specular) * light.color * light.intensity * attenuation * coneFalloff;
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
    vec3 heightDir = normalize(light.tangent);

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

    // the intensity is in candela like every local light, so the emitter's luminance times its area is already folded in
    // only the emission cosine and the area-softened inverse square remain.
    float area = light.sizeX * light.sizeY;
    float solidAngle = lightNdotL / (distance * distance + area);
    float attenuation = CalculateAttenuation(distance, light.range, 2.0);
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
#endif

// the lit colour of a surface, linear: the ambient, the sun and its shadows, the scene lights of its cluster.
// everything is in world space but viewPos, which places the fragment in the clusters and the shadow cascades
vec3 SceneLighting(vec3 worldPos, vec3 viewPos, vec3 worldNormal, vec3 albedo, vec3 specs, float ao)
{
    vec3 L = normalize(uSunDirection);
    float NdotL = max(dot(worldNormal, L), 0.0);

    float shadow = 0.0;
    if (useShadows && NdotL > 0.0)
    {
        shadow = CalculateSunShadow(worldPos, worldNormal, NdotL, -viewPos.z);
    }

    // Ambient lighting: the scene's sky light, one colour from above and one from below
    float ndotUp = clamp(worldNormal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 ambient = mix(uSkyLightLowerColor, uSkyLightColor, ndotUp) * albedo * ao;

    float specular = specs.r;
    float metallic = specs.g;
    float roughness = max(specs.b, 0.02);

    // View direction
    vec3 cameraPos = uInverseViewMatrix[3].xyz;
    vec3 worldV = normalize(cameraPos - worldPos);

    // Fresnel reflectance at normal incidence
    vec3 F0 = mix(vec3(0.08 * specular), albedo, metallic);

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
#ifdef CLUSTERED_LIGHTS
    if (useLighting)
    {
        ClusterData cluster = clusterData[GetClusterIndex(viewPos)];
        for (uint i = 0; i < cluster.count; i++)
        {
            uint lightIndex = lightIndices[cluster.offset + i];
            PerLightData light = lights[lightIndex];
            float fade = LightDistanceFade(light, distance(cameraPos, light.position));
            if (fade <= 0.0) continue;

            uint type = light.flags & LIGHT_TYPE_MASK;
            if (type == 0u) // Point light
            {
                localLighting += CalculatePointLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0) * fade;
            }
            else if (type == 1u) // Spot light
            {
                localLighting += CalculateSpotLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0) * fade;
            }
            else if (type == 2u) // Rect light
            {
                localLighting += CalculateRectLight(light, worldPos, worldNormal, worldV, albedo, metallic, roughness, F0) * fade;
            }
        }
    }
#endif

    return ambient + sunLight + localLighting;
}
