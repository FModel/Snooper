using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class LandscapeSystem() : PrimitiveSystem<Vector3, LandscapeMeshComponent>(50, PrimitiveType.Triangles)
{
    public override uint Order => 23;
    
    protected override ShaderProgram Shader { get; } = new(
"""
#version 460 core
layout (location = 0) in vec3 aPos;

layout(std430, binding = 0) readonly buffer ModelMatrices
{
    mat4 uModelMatrices[];
};

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out float Height;

const float MinHeight = -32.0;
const float MaxHeight = 32.0;

float NormalizeHeight(float y) {
    return clamp((y - MinHeight) / (MaxHeight - MinHeight), 0.0, 1.0);
}

void main()
{
    vec4 worldPos = uModelMatrices[gl_BaseInstance + gl_InstanceID] * vec4(aPos, 1.0);
    gl_Position = uProjectionMatrix * uViewMatrix * worldPos;
    Height = NormalizeHeight(worldPos.y);
}
""",
"""
#version 460 core

in float Height;
out vec4 FragColor;

void main()
{
    vec3 color;
    if (Height < 0.5)
    {
        float t = Height / 0.5;
        color = mix(vec3(0.0, 0.1, 0.4), vec3(0.0, 0.4, 0.8), t);
    }
    else if (Height < 0.55)
    {
        float t = (Height - 0.5) / 0.05;
        color = mix(vec3(0.9, 0.85, 0.6), vec3(0.6, 0.4, 0.2), t);
    }
    else if (Height < 0.7)
    {
        float t = (Height - 0.55) / 0.15;
        color = mix(vec3(0.6, 0.4, 0.2), vec3(0.1, 0.5, 0.1), t);
    }
    else
    {
        float t = clamp((Height - 0.7) / 0.3, 0.0, 1.0);
        color = mix(vec3(0.1, 0.5, 0.1), vec3(0.05, 0.3, 0.05), t);
    }
    FragColor = vec4(color, 1.0);
}
""");

//     protected override ShaderProgram Shader { get; } = new(
// """
// #version 460 core
// layout (location = 0) in vec3 aPos;
//
// out flat int vMatrixIndex;
//
// void main()
// {
//     gl_Position = vec4(aPos, 1.0);
//     vMatrixIndex = gl_BaseInstance + gl_InstanceID;
// }
// """,
// """
// #version 460 core
//
// out vec4 FragColor;
//
// void main()
// {
//    FragColor = vec4(1.0, 0.0, 0.0, 0.9);
// }
// """)
//     {
//         TesselationControl =
// """
// #version 460 core
// layout (vertices = 4) out;
//
// in gl_PerVertex
// {
//     vec4 gl_Position;
//     float gl_PointSize;
//     float gl_ClipDistance[];
// } gl_in[gl_MaxPatchVertices];
//
// in flat int vMatrixIndex[];
// out flat int tcMatrixIndex[];
//
// void main()
// {
//     gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
//     tcMatrixIndex[gl_InvocationID] = vMatrixIndex[gl_InvocationID];
//     
//     if (gl_InvocationID == 0)
//     {
//         gl_TessLevelOuter[0] = 1;
//         gl_TessLevelOuter[1] = 1;
//         gl_TessLevelOuter[2] = 1;
//         gl_TessLevelOuter[3] = 1;
//
//         gl_TessLevelInner[0] = 1;
//         gl_TessLevelInner[1] = 1;
//     }
// }
// """,
//         TesselationEvaluation =
// """
// #version 460 core
// layout (quads, fractional_odd_spacing, ccw) in;
//
// layout(std430, binding = 0) readonly buffer ModelMatrices
// {
//     mat4 uModelMatrices[];
// };
//
// in flat int tcMatrixIndex[];
//
// uniform sampler2D uHeightMap0;
// uniform mat4 uViewMatrix;
// uniform mat4 uProjectionMatrix;
//
// void main()
// {
//     float u = gl_TessCoord.x;
//     float v = gl_TessCoord.y;
//
//     vec4 p00 = gl_in[0].gl_Position;
//     vec4 p01 = gl_in[1].gl_Position;
//     vec4 p10 = gl_in[2].gl_Position;
//     vec4 p11 = gl_in[3].gl_Position;
//
//     // compute patch surface normal
//     vec4 uVec = p01 - p00;
//     vec4 vVec = p10 - p00;
//     vec4 normal = normalize(vec4(cross(vVec.xyz, uVec.xyz), 0));
//
//     // bilinearly interpolate position coordinate across patch
//     vec4 p0 = (p01 - p00) * u + p00;
//     vec4 p1 = (p11 - p10) * u + p10;
//     vec4 p = (p1 - p0) * v + p0;
//
//     // displace point along normal
//     // p += normal * Height;
//
//     gl_Position = uProjectionMatrix * uViewMatrix * uModelMatrices[tcMatrixIndex[0]] * p;
// }
// """
//     };

    protected override void PreRender(CameraComponent camera)
    {
        // _polygonMode = (PolygonMode)GL.GetInteger(GetPName.PolygonMode);
        // _bDiff = _polygonMode != PolygonMode.Line;
        // if (_bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
        
        base.PreRender(camera);
    }
    
    private bool _bDiff;
    private PolygonMode _polygonMode;

    protected override void PostRender(CameraComponent camera)
    {
        // if (_bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, _polygonMode);
    }

    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}