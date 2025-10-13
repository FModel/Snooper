layout (vertices = 4) out;

#include "Buffers/PerInstanceData.glsl"

in gl_PerVertex
{
    vec4 gl_Position;
    float gl_PointSize;
    float gl_ClipDistance[];
} gl_in[gl_MaxPatchVertices];

uniform mat4 uViewMatrix;

in flat uint vInstanceID[];
in flat uint vDrawID[];
out flat uint tcInstanceID[];
out flat uint tcDrawID[];

float getTessLevel(vec4 pos)
{
    // uTessMin = 4.0
    // uTessMax = 128.0
    // uTessNear = 5.0
    // uTessFar = 900.0
    // uFalloffExp = 0.35

    float dist = length(pos.xyz);
    float t = clamp((dist - 5.0) / (900.0 - 5.0), 0.0, 1.0);
    float falloff = 1.0 - pow(t, 0.35);

    return mix(4.0, 128.0, falloff);
}

void main()
{
    gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
    tcInstanceID[gl_InvocationID] = vInstanceID[gl_InvocationID];
    tcDrawID[gl_InvocationID] = vDrawID[gl_InvocationID];

    if (gl_InvocationID == 0)
    {
        vec4 eyeSpacePos00 = uViewMatrix * uInstanceDataBuffer[vInstanceID[0]].Matrix * gl_in[0].gl_Position;
        vec4 eyeSpacePos01 = uViewMatrix * uInstanceDataBuffer[vInstanceID[1]].Matrix * gl_in[1].gl_Position;
        vec4 eyeSpacePos10 = uViewMatrix * uInstanceDataBuffer[vInstanceID[2]].Matrix * gl_in[2].gl_Position;
        vec4 eyeSpacePos11 = uViewMatrix * uInstanceDataBuffer[vInstanceID[3]].Matrix * gl_in[3].gl_Position;

        float tessLevel0 = getTessLevel(eyeSpacePos00);
        float tessLevel1 = getTessLevel(eyeSpacePos01);
        float tessLevel2 = getTessLevel(eyeSpacePos10);
        float tessLevel3 = getTessLevel(eyeSpacePos11);

        gl_TessLevelOuter[0] = tessLevel0;
        gl_TessLevelOuter[1] = tessLevel1;
        gl_TessLevelOuter[2] = tessLevel2;
        gl_TessLevelOuter[3] = tessLevel3;

        gl_TessLevelInner[0] = max(tessLevel1, tessLevel3);
        gl_TessLevelInner[1] = max(tessLevel0, tessLevel2);
    }
}