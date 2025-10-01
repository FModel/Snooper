in vec2 vTexCoords;

uniform usampler2D pickingTexture;
uniform uint picked;

out vec4 FragColor;

void main()
{
    if (picked == 0u)
    {
        discard; // Background, no object
    }
    
    uint id = texture(pickingTexture, vTexCoords).r;
    FragColor = vec4(id == picked ? 1.0 : 0.0, 0.0, 0.0, 1.0);
}
