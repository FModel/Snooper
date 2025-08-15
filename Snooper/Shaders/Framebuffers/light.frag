in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gColor;
uniform sampler2D gSpecular;
uniform sampler2D ssao;

uniform bool useSsao;
uniform mat4 uViewMatrix;

out vec4 FragColor;

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

void main()
{
    mat3 viewMat = mat3(uViewMatrix);
    
    vec3 position = texture(gPosition, vTexCoords).rgb;
    vec3 normal = viewMat * texture(gNormal, vTexCoords).rgb;
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

    vec3 lightDirs[3] = vec3[3](
        normalize(viewMat * vec3(0.5, 1.0, 0.3)),  // Key light
        normalize(viewMat * vec3(-0.3, 0.5, 0.8)), // Fill light
        normalize(viewMat * vec3(0.0, -0.5, -1.0))  // Back light
    );
    float lightIntensity[3] = float[3](1.0, 0.2, 0.2);
    vec3 lightColors = vec3(1.0, 0.95, 0.9);

    vec3 totalLight = vec3(0.0);
    for (int i = 0; i < 3 ; i++)
    {
        vec3 L = lightDirs[i];
        vec3 H = normalize(V + L);
        float NdotL = max(dot(normal, L), 0.0);

        vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);
        float D = DistributionGGX(normal, H, roughness);
        float G = GeometrySmith(normal, V, L, roughness);

        vec3 spec = (D * G * F) / (4.0 * max(dot(normal, V), 0.001) * NdotL + 0.001);
        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metallic;
        vec3 diff = kD * albedo / PI;

        totalLight += (diff + spec) * lightColors * NdotL * lightIntensity[i];
    }
    
    vec3 color = totalLight + ambient;

    color = pow(color, vec3(1.0 / 2.2));
    FragColor = vec4(color, 1.0);
}