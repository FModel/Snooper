using System.Numerics;
using Snooper.Rendering.Components;

namespace Snooper.Rendering.Systems;

public class DebugSystem : PrimitiveSystem<Vector3, DebugComponent>
{
    public override uint Order { get => 100; }
    protected override bool AllowDerivation { get => true; }

    public override void Load()
    {
        Shader.FragmentShaderCode =
"""
#version 330 core

out vec4 FragColor;

void main()
{
    FragColor = vec4(1.0f, 1.0f, 1.0f, 0.5f);
}
""";

        base.Load();
    }
}
