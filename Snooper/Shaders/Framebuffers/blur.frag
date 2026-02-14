in vec2 vTexCoords;

uniform sampler2D inputTexture;
uniform vec2 texelSize;
uniform int blurRadius;

out vec4 FragColor;

void main()
{
    vec4 result = vec4(0.0);
    float totalWeight = 0.0;

    for (int x = -blurRadius; x <= blurRadius; x++)
    {
        for (int y = -blurRadius; y <= blurRadius; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            result += texture(inputTexture, vTexCoords + offset);
            totalWeight += 1.0;
        }
    }

    FragColor = result / totalWeight;
}
