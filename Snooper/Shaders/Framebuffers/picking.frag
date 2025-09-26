in vec2 vTexCoords;

uniform sampler2D outlineMask;
uniform vec3 outlineColor;

out vec4 FragColor;

void main()
{
    float edge = texture(outlineMask, vTexCoords).r;

    if (edge > 0.5)
    {
        FragColor = vec4(outlineColor, 1.0);
    }
    else
    {
        discard;
    }
}
