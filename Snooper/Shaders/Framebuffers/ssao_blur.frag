in vec2 vTexCoords;

uniform sampler2D aoInput;

out float FragColor;

void main()
{
    vec2 texelSize = 1.0 / vec2(textureSize(aoInput, 0));
    
    float result = 0.0;
    float totalWeight = 0.0;
    
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            result += texture(aoInput, vTexCoords + offset).r;
            totalWeight += 1.0;
        }
    }
    
    FragColor = result / totalWeight;
}