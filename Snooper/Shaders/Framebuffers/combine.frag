in vec2 vTexCoords;

uniform sampler2D inputTextures[4];
uniform int numInputTextures;

out vec4 FragColor;

void main()
{
    vec4 color = texture(inputTextures[0], vTexCoords);
    for (int i = 1; i < numInputTextures; ++i)
    {
        vec4 current = texture(inputTextures[i], vTexCoords);
        color = mix(color, current, current.a);
    }
    FragColor = color;
}
