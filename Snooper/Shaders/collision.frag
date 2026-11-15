uniform vec3 uPalette[6]; // a colour per ECollisionEnabled
uniform mat4 uViewMatrix;
uniform float uScanOffset;
uniform bool uScanPaused;

const float TintOpacity = 0.1;
const float ScanPeriod = 10.0; // metres between two fronts, so several climb a wall at once
const float ScanTrail = 3.0; // metres the triangles stay lit below a front
const float WireBase = 0.05; // how much of the triangles shows between the fronts
const float PausedWire = 0.25; // and everywhere while paused
const float RimStrength = 1.5;

struct PerMaterialData
{
    bool IsReady;
    uint Type; // the ECollisionEnabled
};

layout(std430, binding = BINDING_MATERIAL_DATA) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

in vec3 vViewPos;

layout (location = 0) out vec4 FragColor;

void main()
{
    vec3 color = uPalette[uMaterialDataBuffer[MaterialSlot(uDrawStatic[gDrawID], FetchCulled(gDrawID))].Type];

    // lit by a fixed light from above, in world space so a face keeps its shade as the camera moves
    vec3 normal = normalize(cross(dFdx(vViewPos), dFdy(vViewPos)));
    vec3 worldNormal = transpose(mat3(uViewMatrix)) * normal;
    color *= 0.25 + 0.75 * max(dot(worldNormal, normalize(vec3(0.3, 1.0, 0.5))), 0.0);
    vec3 hot = mix(color, vec3(1.0), 0.55); // the lines

    // fronts sweeping up through the world, sharp on top and trailing off below, that show the triangles
    vec3 worldPos = transpose(mat3(uViewMatrix)) * (vViewPos - uViewMatrix[3].xyz);
    float behind = (1.0 - fract((worldPos.y - uScanOffset) / ScanPeriod)) * ScanPeriod; // metres below the front
    float scan = uScanPaused ? 0.0 : exp(-behind / ScanTrail);
    float wire = 0.0;
#ifdef WIREFRAME
    vec3 b = gl_BaryCoordNV;
    vec3 a = smoothstep(vec3(0.0), fwidth(b) * 1.5, b);
    wire = (1.0 - min(min(a.x, a.y), a.z)) * (uScanPaused ? PausedWire : max(scan, WireBase));
#endif

    vec3 result = mix(color, hot, wire);
    float alpha = mix(TintOpacity, 0.9, wire);

    // a brighter rim toward the silhouette
    float facing = abs(dot(normal, normalize(vViewPos)));
    result = mix(result, hot, min(pow(1.0 - facing, 3.0) * 0.5 * RimStrength, 1.0));
    alpha = clamp(alpha * mix(1.0 + 0.5 * RimStrength, 1.0, facing), 0.0, 1.0);

    FragColor = vec4(result * alpha, alpha); // premultiplied, see the forward pass blend state
}
