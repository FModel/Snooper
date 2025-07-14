#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;

struct PerInstanceData
{
    mat4 Matrix;
    sampler2D Diffuse;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

uniform int uDebugColorMode;

in flat int mIndex;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

void main()
{
    vec3 color = fs_in.vDebugColor;
    if (uDebugColorMode == 0) color = texture(uInstanceDataBuffer[mIndex].Diffuse, fs_in.vTexCoords).rgb;
    if (uDebugColorMode == 4)
    {
        color = mix(vec3(0.25), vec3(1.0), vec3(
            float((gl_PrimitiveID * 61u) % 255u) / 255.0,
            float((gl_PrimitiveID * 149u) % 255u) / 255.0,
            float((gl_PrimitiveID * 233u) % 255u) / 255.0
        ));
    }

    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    gColor.rgb = color;
    gColor.a = 1.0;
}