using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class RenderSystem : PrimitiveSystem<MeshComponent>
{
    public override uint Order { get => 21; }
    protected override bool AllowDerivation { get => true; }

    public override void Load()
    {
        Shader.FragmentShaderCode = @"#version 330 core
out vec4 FragColor;

void main()
{
    FragColor = vec4(1.0f, 0.0f, 0.0f, 1.0f);
}";

        base.Load();
    }
}
