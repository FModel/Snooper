in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gColor;
uniform sampler2D gSpecular;
uniform sampler2D ssao;

uniform bool useSsao;

out vec4 FragColor;

void main()
{
    vec3 position = texture(gPosition, vTexCoords).rgb;
    vec3 normal = texture(gNormal, vTexCoords).rgb;
    vec3 baseColor = texture(gColor, vTexCoords).rgb;
    vec3 specularColor = texture(gColor, vTexCoords).rgb;
    float ao = useSsao ? texture(ssao, vTexCoords).r : 1.0;

    // Hemispheric lighting (sky vs ground)
    vec3 skyColor = vec3(1.0);     // white sky
    vec3 groundColor = vec3(0.5);  // dark gray ground

    float ndotUp = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 hemiLight = mix(groundColor, skyColor, ndotUp);

    // Distance fade to give sense of depth
    float depthFade = mix(1.0, 0.7, smoothstep(10.0, 100.0, length(position)));

    // Combine
    vec3 litColor = baseColor * hemiLight * ao * depthFade;

    FragColor = vec4(litColor, 1.0);
}