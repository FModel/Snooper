in vec2 vTexCoords;

uniform sampler2D texture1;
uniform sampler2D texture2;
uniform bool enabled;
uniform float split;

out vec4 FragColor;

void main()
{
    if (!enabled)
    {
        FragColor = texture(texture1, vTexCoords);
        return;
    }

    if (abs(vTexCoords.x - split) < 0.001)
    {
        FragColor = vec4(1.0, 1.0, 0.0, 1.0);
        return;
    }

    if (vTexCoords.x < split)
        FragColor = texture(texture1, vTexCoords);
    else
        FragColor = texture(texture2, vTexCoords);
}
