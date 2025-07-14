in vec2 vTexCoords;

uniform sampler2D ssaoInput;

out float FragColor;

void main()
{
    vec2 texelSize = 1.0 / vec2(textureSize(ssaoInput, 0));
    int intensity = 2;

    float result = 0.0;
    for (int x = -intensity; x < intensity; ++x)
    {
        for (int y = -intensity; y < intensity; ++y)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            result += texture(ssaoInput, vTexCoords + offset).r;
        }
    }

    FragColor = result / (4.0 * 4.0);
}