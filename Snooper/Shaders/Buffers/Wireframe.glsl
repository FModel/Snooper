uniform bool uWireframe;
uniform vec3 uWireframeColor;
uniform float uWireframeWidth; // pixels
uniform bool uWireframeOverlay; // the wire over the shaded surface, else the surface is dropped around it

// how much of the pixel the wire covers: 0 away from every edge, 1 on one, soft over the last pixel
float WireCoverage()
{
#ifdef WIREFRAME
    vec3 b = gl_BaryCoordNV;
    vec3 d = fwidth(b);
    vec3 a = smoothstep(d * max(uWireframeWidth - 1.0, 0.0), d * uWireframeWidth, b);
    return 1.0 - min(min(a.x, a.y), a.z);
#else
    return 0.0;
#endif
}

// what the pixel ends up as under the wire: the wire colour by its coverage, unlit where the wire is; in cage mode
// nothing at all away from it, and the coverage feeds the opacity so the forward pass blends the edge
void ApplyWire(inout vec3 color, inout bool unlit, inout float opacity)
{
#ifdef WIREFRAME
    if (!uWireframe) return;

    float wire = WireCoverage();
    if (!uWireframeOverlay)
    {
        if (wire < 0.05) discard;
        opacity *= wire;
    }

    color = mix(color, uWireframeColor, wire);
    unlit = unlit || wire > 0.5;
#endif
}
