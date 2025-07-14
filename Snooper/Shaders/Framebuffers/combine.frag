in vec2 vTexCoords;

uniform sampler2D deferredTexture;
uniform sampler2D forwardTexture;

out vec4 FragColor;

void main()
{
    vec4 deferredColor = texture(deferredTexture, vTexCoords);
    vec4 forwardColor = texture(forwardTexture, vTexCoords);

    FragColor = mix(deferredColor, forwardColor, forwardColor.a);
}