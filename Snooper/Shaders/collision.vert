layout (location = 0) in vec3 aPos;

#include "Buffers/common.vert"
#include "Buffers/PerInstanceData.glsl"
#include "Buffers/PerDrawData.glsl"

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out vec3 vViewPos;
flat out float vStripeSpacing;

const float StripeSpacing = 0.1; // metres, at most
const float StripesPerShell = 10.0; // along its largest side, for the shells too small for the spacing above

void main()
{
    SetCommonVSOut();

    mat4 matrix = uInstanceDataBuffer[gl_BaseInstance + gl_InstanceID].Matrix;
    vec4 viewPos = uViewMatrix * matrix * vec4(aPos, 1.0);
    vViewPos = viewPos.xyz;

    PerDrawStatic draw = uDrawStatic[gl_DrawID];
    vec3 extents = vec3(draw.BoundsExtents[0], draw.BoundsExtents[1], draw.BoundsExtents[2]) * vec3(length(matrix[0].xyz), length(matrix[1].xyz), length(matrix[2].xyz));
    vStripeSpacing = clamp(2.0 * max(max(extents.x, extents.y), extents.z) / StripesPerShell, 0.005, StripeSpacing);

    gl_Position = uProjectionMatrix * viewPos;
}
