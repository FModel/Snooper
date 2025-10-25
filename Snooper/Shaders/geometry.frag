#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;
layout (location = 4) out uint gPicking;

#include "material_sampling.glsl"

uniform mat4 uViewMatrix;
uniform int uDebugColorMode;

#include "Buffers/PerDrawCommand.glsl"
#include "Buffers/common.frag"

flat in uint vTexLayer;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    vec4 vColor;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

void main()
{
    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];
    
    vec3 color = fs_in.vDebugColor;
    vec3 spec = vec3(1.0);
    vec3 normal = vec3(0.0, 0.0, 1.0);
    
    if (uDebugColorMode == 0 && materialData.IsReady)
    {
        LayerData layerData = SampleLayer(materialData, vTexLayer, fs_in.vTexCoords);
        
        color = layerData.diffuse.rgb;
        spec = layerData.specular;
        normal = layerData.normal;
    }
    else if (uDebugColorMode == 4)
    {
        color = mix(vec3(0.25), vec3(1.0), vec3(
            float((gl_PrimitiveID * 61u) % 255u) / 255.0,
            float((gl_PrimitiveID * 149u) % 255u) / 255.0,
            float((gl_PrimitiveID * 233u) % 255u) / 255.0
        ));
    }
    else if (uDebugColorMode == 5)
    {
        color = fs_in.vColor.rgb;
    }


    gPosition = fs_in.vViewPos;
    gNormal = mat3(uViewMatrix) * normalize(fs_in.TBN * normal);
    gColor.rgb = color;
    gColor.a = 1.0; // free space
    gSpecular.rgb = spec.rgb;
    gSpecular.a = 1.0; // free space
    gPicking = cmd.PickingId;
}