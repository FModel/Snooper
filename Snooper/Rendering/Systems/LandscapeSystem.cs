using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class LandscapeSystem() : PrimitiveSystem<Vector2, LandscapeMeshComponent, PerInstanceLandscapeData>(100, PrimitiveType.Patches)
{
    public override uint Order => 21;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    protected override int BatchCount => int.MaxValue;
    protected override Action<ArrayBuffer<Vector2>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
    
    protected override ShaderProgram Shader { get; } = new(
"""
#version 460 core
layout (location = 0) in vec2 aPos;

uniform float uGlobalScale;

out flat int vMatrixIndex;
out flat int vDrawID;

void main()
{
    gl_Position = vec4(aPos.x * uGlobalScale, 0.0, aPos.y * uGlobalScale, 1.0);
    vMatrixIndex = gl_BaseInstance + gl_InstanceID;
    vDrawID = gl_DrawID;
}
""",
"""
#version 460 core
layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;

in TE_OUT {
    vec3 vViewPos;
    float vHeight;
    mat3 TBN;
} fs_in;

void main()
{
    vec3 color;
    float height = fs_in.vHeight;

    if (height < -0.15) {
        float t = clamp((height + 0.25) / 0.1, 0.0, 1.0);
        color = mix(vec3(0.0, 0.02, 0.1), vec3(0.0, 0.1, 0.4), t);
    }
    else if (height < -0.05) {
        float t = (height + 0.15) / 0.1;
        color = mix(vec3(0.0, 0.1, 0.4), vec3(0.0, 0.4, 0.8), t);
    }
    else if (height < 0.0) {
        float t = (height + 0.05) / 0.05;
        color = mix(vec3(0.0, 0.4, 0.8), vec3(0.9, 0.85, 0.6), t);
    }
    else if (height < 0.1) {
        float t = height / 0.1;
        color = mix(vec3(0.9, 0.85, 0.6), vec3(0.6, 0.4, 0.2), t);
    }
    else if (height < 0.3) {
        float t = (height - 0.1) / 0.2;
        color = mix(vec3(0.6, 0.4, 0.2), vec3(0.1, 0.5, 0.1), t);
    }
    else if (height < 0.85) {
        float t = (height - 0.3) / 0.55;
        color = mix(vec3(0.1, 0.5, 0.1), vec3(0.3, 0.3, 0.3), t);
    }
    else {
        float t = clamp((height - 0.85) / 0.15, 0.0, 1.0);
        color = mix(vec3(0.3, 0.3, 0.3), vec3(1.0, 1.0, 1.0), t);
    }

    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    gColor.rgb = color;
    gColor.a = 1.0;
}
""")
    {
        TessellationControl =
"""
#version 460 core
#extension GL_ARB_bindless_texture : require
layout (vertices = 4) out;

struct PerInstanceData
{
    mat4 Matrix;
    sampler2D Heightmap;
    vec2 ScaleBias;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

in gl_PerVertex
{
    vec4 gl_Position;
    float gl_PointSize;
    float gl_ClipDistance[];
} gl_in[gl_MaxPatchVertices];

uniform mat4 uViewMatrix;

in flat int vMatrixIndex[];
in flat int vDrawID[];
out flat int tcMatrixIndex[];
out flat int tcDrawID[];

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
    tcMatrixIndex[gl_InvocationID] = vMatrixIndex[gl_InvocationID];
    tcDrawID[gl_InvocationID] = vDrawID[gl_InvocationID];
    
    if (gl_InvocationID == 0)
    {
        vec4 eyeSpacePos00 = uViewMatrix * uInstanceDataBuffer[vMatrixIndex[0]].Matrix * gl_in[0].gl_Position;
        vec4 eyeSpacePos01 = uViewMatrix * uInstanceDataBuffer[vMatrixIndex[1]].Matrix * gl_in[1].gl_Position;
        vec4 eyeSpacePos10 = uViewMatrix * uInstanceDataBuffer[vMatrixIndex[2]].Matrix * gl_in[2].gl_Position;
        vec4 eyeSpacePos11 = uViewMatrix * uInstanceDataBuffer[vMatrixIndex[3]].Matrix * gl_in[3].gl_Position;

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
""",
        TessellationEvaluation =
"""
#version 460 core
#extension GL_ARB_bindless_texture : require
layout (quads, fractional_odd_spacing, ccw) in;

struct PerInstanceData
{
    mat4 Matrix;
    sampler2D Heightmap;
    vec2 ScaleBias;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

layout(std430, binding = 1) restrict readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

in flat int tcMatrixIndex[];
in flat int tcDrawID[];

uniform float uSizeQuads;
uniform float uQuadCount;
uniform float uGlobalScale;
uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out TE_OUT {
    vec3 vViewPos;
    float vHeight;
    mat3 TBN;
} te_out;

void main()
{
    PerInstanceData instanceData = uInstanceDataBuffer[tcMatrixIndex[0]];
    vec2 heightmapSize = textureSize(instanceData.Heightmap, 0);
    vec2 texelSize = 1.0 / heightmapSize;
    vec2 componentUvSize = vec2(uSizeQuads) / heightmapSize;
    float quadFraction = 1.0 / uQuadCount;
    
    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;
    float u = gl_TessCoord.x;
    float v = gl_TessCoord.y;
    vec2 uv = instanceData.ScaleBias + subPatchOffset * componentUvSize + vec2(u, v) * (componentUvSize * quadFraction);
    uv = uv * (1.0 - texelSize) + 0.5 * texelSize;

    vec4 color = texture(instanceData.Heightmap, uv);
    float R = color.b * 255.0;
    float G = color.g * 255.0;
    te_out.vHeight = ((R * 256.0) + G - 32768.0) / 128.0 * uGlobalScale;

    float nx = 2.0 * color.r - 1.0;
    float ny = 2.0 * color.a - 1.0;
    float nz = sqrt(max(0.0, 1.0 - (nx * nx + ny * ny)));
    te_out.TBN = mat3(normalize(vec3(-nz, 0.0, nx)), normalize(vec3(0.0, nz, -ny)), normalize(vec3(nx, ny, nz)));

    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;

    vec4 p0 = (p01 - p00) * u + p00;
    vec4 p1 = (p11 - p10) * u + p10;
    vec4 p = (p1 - p0) * v + p0;

    // displace point along normal
    vec4 normal = normalize(vec4(cross((p10 - p00).xyz, (p01 - p00).xyz), 0));
    p += normal * te_out.vHeight;

    vec4 viewPos = uViewMatrix * instanceData.Matrix * p;
    gl_Position = uProjectionMatrix * viewPos;
    te_out.vViewPos = viewPos.xyz;
}
"""
    };
    
    private readonly ShaderStorageBuffer<Vector2> _scales = new(100 * Settings.TessellationQuadCountTotal);

    public override void Load()
    {
        base.Load();

        var sizeQuads = 0.0f;

        _scales.Generate();
        _scales.Bind();
        foreach (var component in Components)
        {
            _scales.AddRange(component.Scales);
            sizeQuads = Math.Max(sizeQuads, component.SizeQuads);
        }
        _scales.Unbind();
        
        Shader.Use();
        Shader.SetUniform("uSizeQuads", sizeQuads);
        Shader.SetUniform("uQuadCount", (float)Settings.TessellationQuadCount);
        Shader.SetUniform("uGlobalScale", Settings.GlobalScale);
    }

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
    
        _scales.Bind(1);
    }
}