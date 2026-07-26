#extension GL_ARB_bindless_texture : require

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;
layout (location = 4) out uint gPicking;

uniform mat4 uViewMatrix;

#include "Buffers/CommonMesh.frag"

void main()
{
    PerDrawData draw = uDrawDataBuffer[gDrawID];
    Surface surface = ResolveSurface(draw, uMaterialDataBuffer[draw.BaseMaterial + draw.MaterialIndex]);

    gPosition = fs_in.vViewPos;
    gNormal = mat3(uViewMatrix) * surface.Normal;
    gColor.rgb = surface.Color;
    gColor.a = 1.0; // free space
    gSpecular.rgb = surface.Specular;
    gSpecular.a = 1.0; // free space
    gPicking = draw.PickingId;
}
