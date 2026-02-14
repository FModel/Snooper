in vec2 vTexCoords;

uniform sampler2D deferredTexture;
uniform sampler2D forwardTexture;
uniform sampler2D maskTexture;
uniform vec2 texelSize; // 1.0 / screen size
uniform int outlineThickness; // pixels
uniform vec3 outlineColor;

out vec4 FragColor;

void main()
{
    vec4 deferred = texture(deferredTexture, vTexCoords);
    vec4 forward = texture(forwardTexture, vTexCoords);
    vec4 color = mix(deferred, forward, forward.a);

    float mask = texture(maskTexture, vTexCoords).r;
    if (mask <= 0.0 || mask >= 1.0)
    {
        bool nearMesh = false;
        for (int y = -outlineThickness; y <= outlineThickness && !nearMesh; ++y)
        {
            for (int x = -outlineThickness; x <= outlineThickness; ++x)
            {
                if (x == 0 && y == 0) continue;

                vec2 offset = vec2(float(x), float(y)) * texelSize;
                float neighbor = texture(maskTexture, vTexCoords + offset).r;

                if (neighbor > 0.0 && neighbor < 1.0)
                {
                    nearMesh = true;
                    break;
                }
            }
        }

        if (nearMesh)
        {
            color = vec4(outlineColor, 1.0);
        }
    }

    FragColor = color;
}
