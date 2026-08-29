layout (location = 1) out uint gPicking;

#include "Grid/grid.glsl"

out vec4 FragColor;

void main()
{
    GridHit hit;
    if (!TraceGrid(hit)) discard;
    if (hit.fade <= 0.0) discard;

    gl_FragDepth = ComputeDepth(hit.position);
    gPicking = 0u;

    float minor = MinorCoverage(hit);
    float major = MajorCoverage(hit);

    // major lines sit on top of minor ones, they share the same positions
    vec4 color = vec4(uMinorColor, minor * uMinorOpacity);
    color = mix(color, vec4(uMajorColor, major * uMajorOpacity), major);
    color = ApplyAxes(color, hit, 1.0);

    float coverage = color.a * hit.fade * uOpacity;
    if (coverage <= 0.0) discard;

    FragColor = vec4(color.rgb * uColor * coverage, coverage); // premultiplied, see the forward pass blend state
}
