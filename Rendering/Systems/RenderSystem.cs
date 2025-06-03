using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Systems;

public class RenderSystem : PrimitiveSystem<Vertex, MeshComponent>
{
    public override uint Order { get => 21; }
    protected override bool AllowDerivation { get => true; }

    public override void Load()
    {
        Shader.VertexShaderCode =
"""
#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec3 aTangent;
layout (location = 3) in vec2 aTexCoords;

uniform mat4 uModelMatrix;
uniform mat4 uViewProjectionMatrix;

out VS_OUT {
    vec2 vTexCoords;
    mat3 TBN;
} vs_out;

vec3 calculateBitangent(vec3 normal, vec3 tangent)
{
    vec3 N = normalize(normal);
    vec3 T = normalize(tangent);
    return cross(N, T);
}

void main()
{
    gl_Position = uViewProjectionMatrix * uModelMatrix * vec4(aPos, 1.0);

    vec3 T = normalize(vec3(uModelMatrix * vec4(aTangent,   0.0)));
    vec3 B = normalize(vec3(uModelMatrix * vec4(calculateBitangent(aNormal, aTangent), 0.0)));
    vec3 N = normalize(vec3(uModelMatrix * vec4(aNormal,    0.0)));

    vs_out.vTexCoords = aTexCoords;
    vs_out.TBN = mat3(T, B, N);
}
""";
        Shader.FragmentShaderCode =
"""
#version 330 core

in VS_OUT {
    vec2 vTexCoords;
    mat3 TBN;
} fs_in;

out vec4 FragColor;

void main()
{
    vec3 lightDir = normalize(vec3(1.0, 1.0, -1.0));

    vec3 normal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    float diff = max(dot(normal, lightDir), 0.5);

    vec3 finalColor = vec3(1.0) * diff;

    FragColor = vec4(finalColor, 1.0);
}
""";

        base.Load();
    }
}
