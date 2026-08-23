uniform sampler2D in_fontTexture;
uniform int in_channelSwizzle;
uniform bool in_encodeSrgb;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    vec4 texel = texture(in_fontTexture, texCoord);

    if (in_channelSwizzle >= 0)
    {
        texel = vec4(vec3(texel[in_channelSwizzle]), 1.0);
    }

    if (in_encodeSrgb)
    {
        texel.rgb = pow(texel.rgb, vec3(1.0 / 2.2));
    }

    outputColor = color * texel;
}
