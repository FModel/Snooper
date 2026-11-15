uniform vec3 uPalette[6]; // a colour per ECollisionEnabled
uniform mat4 uViewMatrix;
uniform float uStripePhase;

const float StripeOpacity = 0.5;
const float GapOpacity = 0.1;

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
flat in float vStripeSpacing;

layout (location = 0) out vec4 FragColor;

void main()
{
    vec3 color = uPalette[uMaterialDataBuffer[MaterialSlot(uDrawStatic[gDrawID], FetchCulled(gDrawID))].Type];

    // lit by a fixed light from above, in world space so a face keeps its shade as the camera moves
    vec3 normal = normalize(cross(dFdx(vViewPos), dFdy(vViewPos)));
    vec3 worldNormal = transpose(mat3(uViewMatrix)) * normal;
    color *= 0.45 + 0.55 * max(dot(worldNormal, normalize(vec3(0.3, 1.0, 0.5))), 0.0);

    // diagonal bands in world space, averaged out where they get thinner than a pixel, thicker toward the silhouette
    vec3 worldPos = transpose(mat3(uViewMatrix)) * (vViewPos - uViewMatrix[3].xyz);
    float s = dot(worldPos, vec3(0.57735)) / vStripeSpacing - uStripePhase;
    float w = fwidth(s);
    float band = smoothstep(0.5 - w, 0.5 + w, abs(fract(s) - 0.5) * 2.0);
    band = mix(band, 0.5, smoothstep(0.25, 0.5, w));
    float facing = abs(dot(normal, normalize(vViewPos)));
    float alpha = clamp(mix(GapOpacity, StripeOpacity, band) * mix(1.5, 1.0, facing), 0.0, 1.0);

    FragColor = vec4(color * alpha, alpha); // premultiplied, see the forward pass blend state
}
