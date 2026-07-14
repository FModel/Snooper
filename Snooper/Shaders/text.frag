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
    PerDrawData draw = uDrawDataBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[draw.BaseMaterial + draw.MaterialIndex];
    
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
    
    FragColor = text * vec4(color, 1.0);
    
    gPicking = draw.PickingId;
}