const float PI = 3.14159265359;

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r*r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx1 = GeometrySchlickGGX(NdotV, roughness);
    float ggx2 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

vec3 FresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

vec3 EvaluatePBR(
    vec3 albedo,
    vec3 normal,
    vec3 V,
    float metallic,
    float roughness,
    vec3 F0,
    int lightCount,
    vec3 lightDirs[3],
    vec3 lightColors[3],
    float lightIntensity[3],
    vec3 ambient
)
{
    float NdotV = max(dot(normal, V), 0.001);
    vec3 totalLight = vec3(0.0);
    for (int i = 0; i < lightCount; i++)
    {
        vec3 L = normalize(lightDirs[i]);
        vec3 H = normalize(V + L);
        float NdotL = max(dot(normal, L), 0.0);

        vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);
        float D = DistributionGGX(normal, H, roughness);
        float G = GeometrySmith(normal, V, L, roughness);

        vec3 spec = (D * G * F) / (4.0 * NdotV * NdotL + 0.001);
        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metallic;
        vec3 diff = kD * albedo / PI;

        totalLight += (diff + spec) * lightColors[i] * NdotL * lightIntensity[i];
    }

    return totalLight + ambient;
}