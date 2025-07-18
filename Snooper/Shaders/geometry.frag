#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;

struct PerDrawData
{
    bool IsReady;
    sampler2D Diffuse;
    sampler2D Normal;
};

layout(std430, binding = 1) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
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
    PerDrawData drawData = uDrawDataBuffer[mIndex];
    
    vec3 color = fs_in.vDebugColor;
    if (uDebugColorMode == 0 && drawData.IsReady)
    {
        color = texture(drawData.Diffuse, fs_in.vTexCoords).rgb;
    }
    else if (uDebugColorMode == 4)
    {
        color = mix(vec3(0.25), vec3(1.0), vec3(
            float((gl_PrimitiveID * 61u) % 255u) / 255.0,
            float((gl_PrimitiveID * 149u) % 255u) / 255.0,
            float((gl_PrimitiveID * 233u) % 255u) / 255.0
        ));
    }
    
    vec3 normal = vec3(0.0, 0.0, 1.0);
    if (drawData.IsReady)
    {
        normal = texture(drawData.Normal, fs_in.vTexCoords).rgb * 2.0 - 1.0;
    }

    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * normal);
    gColor.rgb = color;
    gColor.a = 1.0;
}