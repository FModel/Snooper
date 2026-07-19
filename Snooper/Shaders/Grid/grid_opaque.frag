// Infinite ground plane written into the gbuffer, so it is lit and shadowed like any other opaque
// surface. Same ray traced plane as grid.frag, but the pattern ends up in the albedo target instead
// of being blended over the frame, and the plane itself is never transparent.

layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;
layout (location = 4) out uint gPicking;

#include "Grid/grid.glsl"

uniform vec3 uCheckerColorA;
uniform vec3 uCheckerColorB;
uniform float uCheckerScale; // checker squares per major division
uniform float uRoughness;
uniform float uMetallic;

void main()
{
    GridHit hit;
    if (!TraceGrid(hit)) discard; // the plane is not visible here, let the skybox through

    gl_FragDepth = ComputeDepth(hit.position);

    float checker = CheckerCoverage(hit.position.xz, hit.ddx, hit.ddy, hit.majorSize / max(uCheckerScale, 1e-4));
    vec4 color = vec4(mix(uCheckerColorA, uCheckerColorB, checker), 1.0);

    // the lines fade with distance, the surface underneath stays opaque all the way to the horizon
    float minor = MinorCoverage(hit) * uMinorOpacity * hit.fade;
    float major = MajorCoverage(hit) * uMajorOpacity * hit.fade;
    color = mix(color, vec4(uMinorColor, 1.0), minor);
    color = mix(color, vec4(uMajorColor, 1.0), major);
    color = ApplyAxes(color, hit, hit.fade);

    gPosition = (uViewMatrix * vec4(hit.position, 1.0)).xyz;
    gNormal = mat3(uViewMatrix) * vec3(0.0, 1.0, 0.0); // the plane always faces straight up
    gColor = vec4(color.rgb * uColor, 1.0);
    gSpecular = vec4(0.0, uMetallic, uRoughness, 1.0);
    gPicking = 0u;
}
