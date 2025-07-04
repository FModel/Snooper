using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class RenderSystem() : PrimitiveSystem<Vertex, MeshComponent, PerInstanceData>(500)
{
    public override uint Order => 22;
    protected override bool AllowDerivation => true;
    protected override int BatchCount => int.MaxValue;
    protected override Action<ArrayBuffer<Vertex>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, buffer.Stride, 12);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, buffer.Stride, 24);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, buffer.Stride, 36);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        GL.EnableVertexAttribArray(2);
        GL.EnableVertexAttribArray(3);
    };

    protected override ShaderProgram Shader { get; } = new(
"""
#version 460 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec3 aTangent;
layout (location = 3) in vec2 aTexCoords;

struct PerInstanceData
{
    mat4 Matrix;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform int uDebugColorMode;

out VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} vs_out;

void main()
{
    int id = gl_BaseInstance + gl_InstanceID;
    mat4 matrix = uInstanceDataBuffer[id].Matrix;
    vec4 viewPos = uViewMatrix * matrix * vec4(aPos, 1.0);
    gl_Position = uProjectionMatrix * viewPos;

    vec3 T = normalize(vec3(matrix * vec4(aTangent,   0.0)));
    vec3 N = normalize(vec3(matrix * vec4(aNormal,    0.0)));
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    vs_out.vViewPos = viewPos.xyz;
    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, normalize(cross(N, T)), N);
    
    vs_out.vDebugColor = vec3(0.75);
    if (uDebugColorMode == 0) return;
    else if (uDebugColorMode == 1)
    {
        id = gl_BaseVertex;
    }
    else if (uDebugColorMode == 3)
    {
        id = gl_DrawID;
    }
    
    vs_out.vDebugColor = mix(vec3(0.25), vec3(1.0), vec3(
        float((id * 97u) % 255u) / 255.0,
        float((id * 59u) % 255u) / 255.0,
        float((id * 31u) % 255u) / 255.0
    ));
}
""",
"""
#version 460 core

in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

out vec4 FragColor;

void main()
{
    vec3 tangent = normalize(fs_in.TBN * vec3(1.0, 0.0, 0.0));
    vec3 bitangent = normalize(fs_in.TBN * vec3(0.0, 1.0, 0.0));
    vec3 normal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));

    float tFactor = 0.9 + 0.1 * tangent.z;
    float bFactor = 0.9 + 0.1 * bitangent.z;
    float nFactor = 0.7 + 0.3 * normal.z;

    float brightness = (tFactor + bFactor + nFactor) / 3.0;
    FragColor = vec4(fs_in.vDebugColor * brightness, 1.0);
}
"""
);

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
        
        Shader.SetUniform("uDebugColorMode", (int)DebugColorMode);
    }
}
