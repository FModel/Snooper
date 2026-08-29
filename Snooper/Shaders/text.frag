layout (location = 1) out uint gPicking;

struct PerMaterialData
{
    bool IsReady;
    vec3 FontColor;
};

layout(std430, binding = BINDING_MATERIAL_DATA) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

in vec2 vTexCoord;

uniform sampler2D uTextTexture;

out vec4 FragColor;

void main()
{
    PerDrawStatic draw = uDrawStatic[gDrawID];
    PerDrawCulled culled = FetchCulled(gDrawID);
    PerMaterialData materialData = uMaterialDataBuffer[draw.BaseMaterial + culled.MaterialIndex];
    
    vec4 text = texture(uTextTexture, vTexCoord);
    if (text.a < 0.1)
    {
        gPicking = 0u;
        discard;
    }
    
    vec3 color = vec3(1.0);
    if (materialData.IsReady)
    {
        color = materialData.FontColor;
    }
    
    FragColor = vec4(text.rgb * color * text.a, text.a); // premultiplied, see the forward pass blend state
    
    gPicking = draw.PickingId;
}