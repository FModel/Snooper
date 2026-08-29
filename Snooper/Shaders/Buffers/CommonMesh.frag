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
    vec3 Normal;   // world space, tangent basis already applied
    float Opacity;
    bool Discard;  // masked out by the material's blend mode
    bool Additive; // contributes light without covering what is behind it
};

vec3 UvGridColor(vec2 uv)
{
    const float cells = 8.0;
    vec2 cell = uv * cells;

    // pixel footprint of one cell, used both for AA width and the distance fade
    vec2 fw = max(fwidth(cell), vec2(1e-5));
    float fade = clamp(1.0 - max(fw.x, fw.y), 0.0, 1.0);

    // checker, AA'd by filtering the square wave over the pixel footprint
    vec2 t = abs(fract(cell * 0.5) - 0.5) * 2.0;
    vec2 checkerAA = clamp((t - 0.5) / fw + 0.5, 0.0, 1.0);
    float checker = mix(checkerAA.x, 1.0 - checkerAA.x, checkerAA.y);
    vec3 color = mix(vec3(0.25), vec3(0.55), checker);

    // cell borders
    vec2 g = abs(fract(cell) - 0.5) / fw;
    float line = 1.0 - clamp(min(g.x, g.y) - 0.5, 0.0, 1.0);
    color = mix(color, vec3(0.85), line * fade);

    // origin axes (u == 0 and v == 0), brighter so tiling direction is readable
    vec2 a = abs(uv) / max(fwidth(uv), vec2(1e-5));
    float axis = 1.0 - clamp(min(a.x, a.y) - 1.0, 0.0, 1.0);
    color = mix(color, vec3(1.0), axis * fade);

    return color;
}

Surface ResolveSurface(PerMaterialData material)
{
    Surface surface;
    surface.Color = fs_in.vFragColor;
    surface.Specular = vec3(0.0, 0.0, 0.6);
    surface.Opacity = 1.0;
    surface.Discard = false;
    surface.Additive = false;

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
            surface.Additive = true;
            surface.Opacity = layer.diffuse.a;
        }

        surface.Color = GetSurfaceColor(material, layer, fs_in.vFragColor);
        surface.Specular = layer.specular;
        normal = layer.normal;
    }

    surface.Normal = normalize(fs_in.TBN * normal);
    if (vColorMode == 6) // Normals
    {
        surface.Color = surface.Normal;
    }
    else if (vColorMode == 10) // UV
    {
        surface.Color = UvGridColor(fs_in.vTexCoords);
    }

    return surface;
}
