using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class LandscapeSystem() : PrimitiveSystem<Vector2, LandscapeMeshComponent>(32, PrimitiveType.Patches)
{
    public override uint Order => 23;
    protected override int BatchCount => 32;

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

in float Height;
out vec4 FragColor;

void main()
{
    vec3 color;

    if (Height < -0.15) {
        float t = clamp((Height + 0.25) / 0.1, 0.0, 1.0);
        color = mix(vec3(0.0, 0.02, 0.1), vec3(0.0, 0.1, 0.4), t);
    }
    else if (Height < -0.05) {
        float t = (Height + 0.15) / 0.1;
        color = mix(vec3(0.0, 0.1, 0.4), vec3(0.0, 0.4, 0.8), t);
    }
    else if (Height < 0.0) {
        float t = (Height + 0.05) / 0.05;
        color = mix(vec3(0.0, 0.4, 0.8), vec3(0.9, 0.85, 0.6), t);
    }
    else if (Height < 0.1) {
        float t = Height / 0.1;
        color = mix(vec3(0.9, 0.85, 0.6), vec3(0.6, 0.4, 0.2), t);
    }
    else if (Height < 0.3) {
        float t = (Height - 0.1) / 0.2;
        color = mix(vec3(0.6, 0.4, 0.2), vec3(0.1, 0.5, 0.1), t);
    }
    else if (Height < 0.85) {
        float t = (Height - 0.3) / 0.55;
        color = mix(vec3(0.1, 0.5, 0.1), vec3(0.3, 0.3, 0.3), t);
    }
    else {
        float t = clamp((Height - 0.85) / 0.15, 0.0, 1.0);
        color = mix(vec3(0.3, 0.3, 0.3), vec3(1.0, 1.0, 1.0), t);
    }

    FragColor = vec4(color, 1.0);
}
""")
    {
        TesselationControl =
"""
#version 460 core
layout (vertices = 4) out;

layout(std430, binding = 0) readonly buffer ModelMatrices
{
    mat4 uModelMatrices[];
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
    float d = smoothstep(20, 1000, length(pos.xyz));
    return mix(128, 16, d);
}

void main()
{
    gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
    tcMatrixIndex[gl_InvocationID] = vMatrixIndex[gl_InvocationID];
    tcDrawID[gl_InvocationID] = vDrawID[gl_InvocationID];
    
    if (gl_InvocationID == 0)
    {
        vec4 eyeSpacePos00 = uViewMatrix * uModelMatrices[vMatrixIndex[0]] * gl_in[0].gl_Position;
        vec4 eyeSpacePos01 = uViewMatrix * uModelMatrices[vMatrixIndex[1]] * gl_in[1].gl_Position;
        vec4 eyeSpacePos10 = uViewMatrix * uModelMatrices[vMatrixIndex[2]] * gl_in[2].gl_Position;
        vec4 eyeSpacePos11 = uViewMatrix * uModelMatrices[vMatrixIndex[3]] * gl_in[3].gl_Position;

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
        TesselationEvaluation =
"""
#version 460 core
layout (quads, fractional_odd_spacing, ccw) in;

layout(std430, binding = 0) readonly buffer ModelMatrices
{
    mat4 uModelMatrices[];
};

layout(std430, binding = 1) readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

in flat int tcMatrixIndex[];
in flat int tcDrawID[];

uniform sampler2D uHeightMaps[32];
uniform float uGlobalScale;
uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out float Height;

void main()
{
    float u = gl_TessCoord.x;
    float v = gl_TessCoord.y;

    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;

    vec4 uVec = p01 - p00;
    vec4 vVec = p10 - p00;
    vec4 normal = normalize(vec4(cross(vVec.xyz, uVec.xyz), 0));

    vec4 p0 = (p01 - p00) * u + p00;
    vec4 p1 = (p11 - p10) * u + p10;
    vec4 p = (p1 - p0) * v + p0;
    
    // i don't really understand this part, it kinda works though
    vec2 textureSize = textureSize(uHeightMaps[tcDrawID[0]], 0);
    vec2 scaleBias = uLandscapeScales[tcMatrixIndex[0]];
    vec2 tileSize = vec2(128.0) / textureSize;
    vec2 uv = vec2(u, v) * tileSize + scaleBias;
    vec2 texelSize = vec2(1.0) / textureSize;
    uv = uv * (1.0 - texelSize) + 0.5 * texelSize;
    
    vec4 color = texture(uHeightMaps[tcDrawID[0]], uv);
    float R = color.b * 255.0;
    float G = color.g * 255.0;
    Height = ((R * 256.0) + G - 32768.0) / 128.0 * uGlobalScale;

    // displace point along normal
    p += normal * Height;

    gl_Position = uProjectionMatrix * uViewMatrix * uModelMatrices[tcMatrixIndex[0]] * p;
}
"""
    };
    
    private readonly ShaderStorageBuffer<Vector2> _scales = new(32);

    public override void Load()
    {
        base.Load();
        
        _scales.Generate();
        _scales.Bind();
        foreach (var component in Components)
        {
            _scales.Add(component.ScaleBias);
        }
        _scales.Unbind();
        
        Shader.Use();
        Shader.SetUniform("uHeightMaps", BatchCount, Enumerable.Range(0, BatchCount).ToArray());
    }

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);

        var unit = TextureUnit.Texture0;
        var batchLimit = Math.Min(BatchCount, ComponentsCount - batchIndex);
        for (var i = 0; i < batchLimit; i++)
        {
            Components.ElementAt(batchIndex + i).Heightmap.Bind(unit);
            unit++;
        }
        
        Shader.SetUniform("uGlobalScale", Settings.GlobalScale);
        _scales.Bind(1);
    }

    protected override Action<ArrayBuffer<Vector2>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}