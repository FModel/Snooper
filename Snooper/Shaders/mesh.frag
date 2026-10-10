#extension GL_ARB_bindless_texture : require

layout (location = 1) out uint gPicking;

#include "Lighting/scene_lighting.glsl"
#include "Lighting/fog.glsl"
#include "Buffers/CommonMesh.frag"
#include "Buffers/Wireframe.glsl"

out vec4 FragColor;

void main()
{
    PerDrawStatic draw = uDrawStatic[gDrawID];
    PerDrawCulled culled = FetchCulled(gDrawID);
    Surface surface = ResolveSurface(MaterialSlot(draw, culled));

    if (surface.Discard)
    {
        discard;
    }

    ApplyWire(surface.Color, surface.Unlit, surface.Opacity);

    // lit and fogged like an opaque surface, there is only no ambient occlusion for it
    vec3 worldPos = (uInverseViewMatrix * vec4(fs_in.vViewPos, 1.0)).xyz;

    vec3 finalColor = surface.Color;
    if (!surface.Unlit)
    {
        finalColor = SceneLighting(worldPos, fs_in.vViewPos, surface.Normal, surface.Color, surface.Specular, 1.0);
    }

    finalColor = pow(finalColor, vec3(1.0 / 2.2));
    float alpha = surface.Additive ? 0.0 : surface.Opacity;
    FragColor = vec4(ApplyFog(finalColor * surface.Opacity, alpha, worldPos), alpha);

    gPicking = draw.PickingId;
}
