#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

struct PerMaterialData
{
    bool IsReady;
    float OpacityMask;
    sampler2D Sprite;
};

layout(std430, binding = BINDING_MATERIAL_DATA) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

in vec2 vTexCoords;

out vec4 FragColor;

void main()
{
    PerDrawStatic draw = uDrawStatic[gDrawID];
    PerDrawCulled culled = FetchCulled(gDrawID);
    PerMaterialData materialData = uMaterialDataBuffer[draw.BaseMaterial + culled.MaterialIndex];

    vec4 color = vec4(1.0);
    if (materialData.IsReady)
    {
        color = texture(materialData.Sprite, vTexCoords);
        if (color.a < materialData.OpacityMask)
        {
            discard;
        }
    }

    vec4 gamma = pow(color, vec4(1.0 / 2.2));
    FragColor = vec4(gamma.rgb * gamma.a, gamma.a); // premultiplied, see the forward pass blend state

    gPicking = draw.PickingId;
}
