in vec2 vTexCoords;

uniform sampler2D gPosition; // view space position
uniform sampler2D gNormal; // view space normal
uniform sampler2D gColor; // albedo color (RGB: albedo, A: 0 = unlit, 1 = lit)
uniform sampler2D gSpecular; // specular color (R: specular, G: metallic, B: roughness, A: unused atm)
uniform sampler2D ssao;

uniform bool useSsao;

out vec4 FragColor;

#include "Lighting/scene_lighting.glsl"
#include "Lighting/fog.glsl"

void main()
{
    vec3 viewPos = texture(gPosition, vTexCoords).rgb;
    vec4 surface = texture(gColor, vTexCoords);
    vec3 worldPos = (uInverseViewMatrix * vec4(viewPos, 1.0)).xyz;

    vec3 color = surface.rgb;
    if (surface.a >= 0.5) // an unlit surface keeps its colour
    {
        vec3 normal = texture(gNormal, vTexCoords).rgb;
        vec3 worldNormal = normalize((uInverseViewMatrix * vec4(normal, 0.0)).xyz);
        vec3 specs = texture(gSpecular, vTexCoords).rgb;
        float ao = useSsao ? texture(ssao, vTexCoords).r : 1.0;

        color = SceneLighting(worldPos, viewPos, worldNormal, surface.rgb, specs, ao);
    }

    // Gamma correction
    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(ApplyFog(color, 1.0, worldPos), 1.0);
}
