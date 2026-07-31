// Fragment stage counterpart of Buffers/CommonMesh.vert: everything geometry.frag and
// mesh.frag share. Owns material_sampling.glsl, Buffers/common.frag and
// Buffers/MeshHooks.glsl (which in turn owns Buffers/PerDrawData.glsl), so the leaf
// fragment shaders must not include any of those again.
//
// The leaf shader still owns "#extension GL_ARB_bindless_texture : require" on its first
// line, since an extension directive has to precede every non-preprocessor token, and its
// own output layout.

#define MESH_FRAGMENT_STAGE

#include "material_sampling.glsl"
#include "Buffers/common.frag"

flat in uint vTexLayer;
flat in uint vColorMode;
in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vFragColor;
} fs_in;

#include "Buffers/MeshHooks.glsl"

struct Surface
{
    vec3 Color;
    vec3 Specular;
    vec3 Normal;  // world space, tangent basis already applied
    float Opacity;
    bool Discard; // masked out by the material's blend mode
};

Surface ResolveSurface(PerDrawData draw, PerMaterialData material)
{
    Surface surface;
    surface.Color = fs_in.vFragColor;
    surface.Specular = vec3(0.0, 0.0, 0.6);
    surface.Opacity = 1.0;
    surface.Discard = false;

    vec3 normal = vec3(0.0, 0.0, 1.0);

    if (vColorMode == 0 && material.IsReady)
    {
        LayerData layer = SampleLayer(material, vTexLayer, fs_in.vTexCoords);

        uint blendMode = GetBlendMode(material);
        if (blendMode == 1u && layer.diffuse.a < 0.3333) // masked
        {
            surface.Discard = true;
        }
        else if (blendMode == 2u) // translucent
        {
            surface.Opacity = layer.diffuse.a;
        }
        else if (blendMode == 3u) // additive
        {
            surface.Opacity = layer.diffuse.r;
        }

        surface.Color = GetSurfaceColor(draw, material, layer, fs_in.vFragColor);
        surface.Specular = layer.specular;
        normal = layer.normal;
    }

    surface.Normal = normalize(fs_in.TBN * normal);
    if (vColorMode == 6) // Normals
    {
        surface.Color = surface.Normal;
    }

    return surface;
}
