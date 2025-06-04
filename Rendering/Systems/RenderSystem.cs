using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class RenderSystem : PrimitiveSystem<Vertex, MeshComponent>
{
    public override uint Order => 21;
    protected override bool AllowDerivation => true;
    
    private readonly ShaderProgram _debug = new("", "");

    public override void Load()
    {
        Shader.Vertex =
"""
#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec3 aTangent;
layout (location = 3) in vec2 aTexCoords;

uniform mat4 uModelMatrix;
uniform mat4 uViewProjectionMatrix;

out VS_OUT {
    vec3 vWorldPos;
    vec2 vTexCoords;
    mat3 TBN;
} vs_out;

void main()
{
    vec4 model = uModelMatrix * vec4(aPos, 1.0);
    gl_Position = uViewProjectionMatrix * model;

    vec3 T = normalize(vec3(uModelMatrix * vec4(aTangent,   0.0)));
    vec3 N = normalize(vec3(uModelMatrix * vec4(aNormal,    0.0)));
    T = normalize(T - dot(T, N) * N); // Gram-Schmidt orthogonalization

    vs_out.vWorldPos = model.xyz;
    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, normalize(cross(N, T)), N);
}
""";
        Shader.Fragment =
"""
#version 330 core

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
""";

        _debug.Vertex = Shader.Vertex;
        _debug.Fragment =
"""
#version 330 core

in vec3 fColor;

out vec4 FragColor;

void main()
{
    FragColor = vec4(fColor, 1.0);
}
""";
        _debug.Geometry =
"""
#version 330 core
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

        base.Load();

        _debug.Generate();
        _debug.Link();
    }

    public override void Render(CameraComponent camera)
    {
        Shader.Use();
        Shader.SetUniform("uViewProjectionMatrix", camera.ViewProjectionMatrix);

        RenderComponents(Shader);

        if (!DebugMode) return;
        
        _debug.Use();
        _debug.SetUniform("uViewProjectionMatrix", camera.ViewProjectionMatrix);

        RenderComponents(_debug);
    }
}
