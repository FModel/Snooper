in vec2 vTexCoords;

uniform sampler2D deferredTexture;
uniform sampler2D forwardTexture;
uniform sampler2D outlineTexture;

out vec4 FragColor;

void main()
{
    vec4 deferredColor = texture(deferredTexture, vTexCoords);
    vec4 forwardColor = texture(forwardTexture, vTexCoords);
    vec4 outlineColor = texture(outlineTexture, vTexCoords);

    vec4 final = mix(deferredColor, forwardColor, forwardColor.a);
    FragColor = mix(final, outlineColor, outlineColor.a);
}