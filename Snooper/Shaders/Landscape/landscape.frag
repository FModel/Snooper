layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;

in TE_OUT {
    vec3 vViewPos;
    float vHeight;
    mat3 TBN;
} fs_in;

void main()
{
    vec3 color;
    float height = fs_in.vHeight;

    if (height < -0.15) {
        float t = clamp((height + 0.25) / 0.1, 0.0, 1.0);
        color = mix(vec3(0.0, 0.02, 0.1), vec3(0.0, 0.1, 0.4), t);
    }
    else if (height < -0.05) {
        float t = (height + 0.15) / 0.1;
        color = mix(vec3(0.0, 0.1, 0.4), vec3(0.0, 0.4, 0.8), t);
    }
    else if (height < 0.0) {
        float t = (height + 0.05) / 0.05;
        color = mix(vec3(0.0, 0.4, 0.8), vec3(0.9, 0.85, 0.6), t);
    }
    else if (height < 0.1) {
        float t = height / 0.1;
        color = mix(vec3(0.9, 0.85, 0.6), vec3(0.6, 0.4, 0.2), t);
    }
    else if (height < 0.3) {
        float t = (height - 0.1) / 0.2;
        color = mix(vec3(0.6, 0.4, 0.2), vec3(0.1, 0.5, 0.1), t);
    }
    else if (height < 0.85) {
        float t = (height - 0.3) / 0.55;
        color = mix(vec3(0.1, 0.5, 0.1), vec3(0.3, 0.3, 0.3), t);
    }
    else {
        float t = clamp((height - 0.85) / 0.15, 0.0, 1.0);
        color = mix(vec3(0.3, 0.3, 0.3), vec3(1.0, 1.0, 1.0), t);
    }

    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    gColor.rgb = color;
    gColor.a = 1.0;
}