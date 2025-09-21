in vec2 vTexCoords;

uniform usampler2D pickingTexture;

out vec4 FragColor;

vec3 hashColor(uint id)
{
    return mix(vec3(0.25), vec3(1.0), vec3(
        float((id * 97u) % 255u) / 255.0,
        float((id * 59u) % 255u) / 255.0,
        float((id * 31u) % 255u) / 255.0
    ));
}

void main()
{
    uint draw = texture(pickingTexture, vTexCoords).r;

    FragColor = vec4(hashColor(draw), 1.0);
}
