in vec2 vTexCoords;

uniform usampler2D deferredPicking;
uniform usampler2D forwardPicking;

out uvec4 FragColor;

void main()
{
    uint deferred = texture(deferredPicking, vTexCoords).r;
    uint forward = texture(forwardPicking, vTexCoords).r;
    
    uint id = 0;
    if (forward != 0u)
        id = forward;
    else if (deferred != 0u)
        id = deferred;

    FragColor = uvec4(id, 0, 0, 0);
}