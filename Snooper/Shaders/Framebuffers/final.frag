in vec2 vTexCoords;

uniform sampler2D texture1;
uniform sampler2D texture2;
uniform bool enabled;
uniform float split;
uniform int channel; // 0 = RGB, 1 = R, 2 = G, 3 = B, 4 = A

out vec4 FragColor;

vec4 IsolateChannel(vec4 color)
{
    if (channel == 1) return vec4(color.rrr, 1.0);
    if (channel == 2) return vec4(color.ggg, 1.0);
    if (channel == 3) return vec4(color.bbb, 1.0);
    if (channel == 4) return vec4(color.aaa, 1.0);
    return color;
}

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
        FragColor = IsolateChannel(texture(texture2, vTexCoords));
}
