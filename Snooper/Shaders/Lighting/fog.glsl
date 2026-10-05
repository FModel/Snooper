// the engine's exponential height fog. Whatever shades a fragment fogs it, at its own distance: the lighting pass,
// a translucent mesh, the skybox. Fogging the blended frame instead cannot tell two translucent layers apart

#include "Lighting/scene_data.glsl"

// the fog between two heights above its base (a <= b): it thins out going up, and stays at its full density below the base
float FogColumn(float a, float b)
{
    float below = max(min(b, 0.0) - min(a, 0.0), 0.0);

    float low = max(a, 0.0);
    float span = max(b, 0.0) - low;
    float above = uFogHeightFalloff * span > 1e-4 ? (1.0 - exp(-uFogHeightFalloff * span)) / uFogHeightFalloff : span;

    return below + exp(-uFogHeightFalloff * low) * above;
}

// integrated along the ray from the camera to a surface that far away
float FogAmount(vec3 direction, float range)
{
    // the fog only starts some way along the ray
    range -= uFogStartDistance;
    if (range <= 0.0) return 0.0;

    float height = uCameraPosition.y + direction.y * uFogStartDistance - uFogHeight;
    float climb = direction.y * range;
    float column = abs(climb) > 1e-3
        ? FogColumn(min(height, height + climb), max(height, height + climb)) / abs(climb)
        : exp(-uFogHeightFalloff * max(height, 0.0));
    return clamp(1.0 - exp(-uFogDensity * range * column), 0.0, uFogMaxOpacity);
}

// one colour everywhere, and a glow toward the sun past some distance
vec3 FogColor(vec3 direction, float range)
{
    vec3 color = uFogColor;
    if (range > uFogDirectionalStart)
    {
        color += uFogDirectionalColor * pow(max(dot(direction, uSunDirection), 0.0), uFogDirectionalExponent);
    }
    return pow(clamp(color, 0.0, 1.0), vec3(1.0 / 2.2));
}

// color is what goes on screen, premultiplied by alpha (1 for an opaque surface)
vec3 ApplyFog(vec3 color, float alpha, vec3 worldPos)
{
    if (!useFog) return color;

    vec3 ray = worldPos - uCameraPosition;
    float range = length(ray);
    if (range <= 0.0) return color;

    vec3 direction = ray / range;
    return mix(color, FogColor(direction, range) * alpha, FogAmount(direction, range));
}

// the sky has no surface: it gets the fog of an endless ray, so the fog bank goes on past the last mesh
vec3 ApplySkyFog(vec3 color, vec3 direction)
{
    if (!useFog) return color;

    return mix(color, FogColor(direction, 1e6), FogAmount(direction, 1e6));
}
