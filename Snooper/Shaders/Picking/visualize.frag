in vec2 vTexCoords;

uniform usampler2D inputTexture;

out vec4 FragColor;

vec3 idToColor(uint id)
{
    uint hash = id * 747796405u + 2891336453u;

    float r = float((hash >> 0u) & 0xFFu) / 255.0;
    float g = float((hash >> 8u) & 0xFFu) / 255.0;
    float b = float((hash >> 16u) & 0xFFu) / 255.0;

    return vec3(r, g, b);
}

void main()
{
    uint id = texture(inputTexture, vTexCoords).r;

    if (id == 0u)
    {
        // Background - transparent or black
        FragColor = vec4(0.0, 0.0, 0.0, 1.0);
    }
    else
    {
        vec3 color = idToColor(id);
        FragColor = vec4(color, 1.0);
    }
}
