struct ShadowViewData
{
    mat4 viewProjection;
    float texelWorldSize; // world units per shadow texel, every derived bias scales off this
    float depthScale;     // projected-depth units per world unit along the light axis
    float splitFar;       // view-space depth where this view stops being the one to sample
    uint slot;            // layer of the shadow depth array
};

layout(std140, binding = BINDING_SHADOW_VIEWS) uniform ShadowViewBuffer
{
    ShadowViewData shadowViews[MAX_SHADOW_VIEWS];
};
