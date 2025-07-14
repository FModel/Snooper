in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gColor;
uniform sampler2D ssao;

uniform bool useSsao;

out vec4 FragColor;

void main()
{
    vec3 position = texture(gPosition, vTexCoords).rgb;
    vec3 normal = texture(gNormal, vTexCoords).rgb;
    vec4 color = texture(gColor, vTexCoords);
    float ao = useSsao ? texture(ssao, vTexCoords).r : 1.0;

    float brightness = 0.7 + 0.3 * normal.z;

    FragColor = vec4(color.rgb * brightness * ao, color.a);
}