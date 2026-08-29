layout (location = 1) out uint gPicking;

#include "Buffers/PerDrawData.glsl"
#include "Buffers/common.frag"

out vec4 FragColor;

void main()
{
    float alpha = 0.75;
    FragColor = vec4(vec3(0.0, 0.0, 1.0) * alpha, alpha); // premultiplied, see the forward pass blend state

    gPicking = uDrawStatic[gDrawID].PickingId;
}
