in vec2 vTexCoords;

uniform sampler2D inputTexture;
uniform vec2 texelSize; // 1.0 / screen size
uniform int outlineThickness; // pixels
uniform vec3 outlineColor;

out vec4 FragColor;

void main()
{
    float depth = texture(inputTexture, vTexCoords).r;
    if (depth > 0.0 && depth < 1.0)
    {
        discard;
    }

    bool nearMesh = false;
    for (int y = -outlineThickness; y <= outlineThickness && !nearMesh; ++y)
    {
        for (int x = -outlineThickness; x <= outlineThickness; ++x)
        {
            if (x == 0 && y == 0) continue;

            vec2 offset = vec2(float(x), float(y)) * texelSize;
            float neighbor = texture(inputTexture, vTexCoords + offset).r;

            if (neighbor > 0.0 && neighbor < 1.0)
            {
                nearMesh = true;
                break;
            }
        }
    }

    if (nearMesh)
    {
        FragColor = vec4(outlineColor, 1.0);
    }
    else
    {
        discard;
    }
}
