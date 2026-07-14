#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;
layout (location = 4) out uint gPicking;

#include "material_sampling.glsl"

uniform mat4 uViewMatrix;
uniform int uFragmentColorMode;

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

flat in uint vTexLayer;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vFragColor;
} fs_in;

void main()
{
    PerDrawData draw = uDrawDataBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[draw.BaseMaterial + draw.MaterialIndex];

    vec3 color = fs_in.vFragColor;
    vec3 spec = vec3(1.0);
    vec3 normal = vec3(0.0, 0.0, 1.0);

    if (uFragmentColorMode == 0 && materialData.IsReady)
    {
        LayerData layerData = SampleLayer(materialData, vTexLayer, fs_in.vTexCoords);

        color = layerData.diffuse.rgb;
        spec = layerData.specular;
        normal = layerData.normal;
    }

    normal = normalize(fs_in.TBN * normal);
    if (uFragmentColorMode == 6) // Normals
    {
        color = normal;
    }

    gPosition = fs_in.vViewPos;
    gNormal = mat3(uViewMatrix) * normal;
    gColor.rgb = color;
    gColor.a = 1.0; // free space
    gSpecular.rgb = spec.rgb;
    gSpecular.a = 1.0; // free space
    gPicking = draw.PickingId;
}
