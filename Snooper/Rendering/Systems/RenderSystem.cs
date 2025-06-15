using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class RenderSystem() : PrimitiveSystem<Vertex, MeshComponent>(500)
{
    public override uint Order => 22;
    protected override bool AllowDerivation => true;

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

layout(std430, binding = 0) buffer ModelMatrices
{
    mat4 uModelMatrices[];
};

uniform mat4 uViewProjectionMatrix;

out VS_OUT {
    vec3 vWorldPos;
    vec2 vTexCoords;
    mat3 TBN;
} vs_out;

void main()
{
    vec4 worldPos = uModelMatrices[gl_DrawID] * vec4(aPos, 1.0);
    gl_Position = uViewProjectionMatrix * worldPos;

    vec3 T = normalize(vec3(vec4(aTangent,   0.0)));
    vec3 N = normalize(vec3(vec4(aNormal,    0.0)));
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    vs_out.vWorldPos = worldPos.xyz;
    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, normalize(cross(N, T)), N);
}
""",
"""
#version 460 core

in VS_OUT {
    vec3 vWorldPos;
    vec2 vTexCoords;
    mat3 TBN;
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
    FragColor = vec4(vec3(0.75) * brightness, 1.0);
}
"""
);

    private readonly ShaderProgram _debug = new("", "");

    public override void Load()
    {
        base.Load();

        _debug.Vertex = Shader.Vertex;
        _debug.Fragment =
"""
#version 460 core

in vec3 fColor;

out vec4 FragColor;

void main()
{
    FragColor = vec4(fColor, 1.0);
}
""";
        _debug.Geometry =
"""
#version 460 core
layout (triangles) in;
layout (line_strip, max_vertices = 6) out;

in VS_OUT {
    vec3 vWorldPos;
    vec2 vTexCoords;
    mat3 TBN;
} gs_in[];

uniform mat4 uViewProjectionMatrix;

out vec3 fColor;

const float MAGNITUDE = 0.01;

void EmitDirection(vec3 worldPos, vec3 dir, vec3 color)
{
    vec4 p0 = uViewProjectionMatrix * vec4(worldPos, 1.0);
    vec4 p1 = uViewProjectionMatrix * vec4(worldPos + dir * MAGNITUDE, 1.0);

    fColor = color;
    gl_Position = p0;
    EmitVertex();

    fColor = color;
    gl_Position = p1;
    EmitVertex();

    EndPrimitive();
}

void GenerateLines(int index)
{
    mat3 TBN = gs_in[index].TBN;
    vec3 pos = gs_in[index].vWorldPos;

    vec3 tangent = normalize(TBN * vec3(1, 0, 0));
    vec3 bitangent = normalize(TBN * vec3(0, 1, 0));
    vec3 normal = normalize(TBN * vec3(0, 0, 1));

    EmitDirection(pos, tangent, vec3(1, 0, 0));    // Red   - Tangent
    EmitDirection(pos, bitangent, vec3(0, 1, 0));  // Green - Bitangent
    EmitDirection(pos, normal, vec3(0, 0, 1));     // Blue  - Normal
}

void main()
{
    for (int i = 0; i < gl_in.length(); i++)
    {
        GenerateLines(i);
    }
}
""";
        _debug.Generate();
        _debug.Link();
    }

    public override void Render(CameraComponent camera)
    {
        base.Render(camera);
        if (!DebugMode) return;

        _debug.Use();
        _debug.SetUniform("uViewProjectionMatrix", camera.ViewProjectionMatrix);
        Resources.Render();
    }
}
