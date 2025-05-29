using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TComponent> : ActorSystem<TComponent> where TComponent : PrimitiveComponent
{
    public override uint Order { get; protected set; } = 20;

    protected virtual ShaderProgram Shader { get; } = new(@"
#version 330 core
layout (location = 0) in vec3 aPos;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = projection * view * model * vec4(aPos, 1.0);
}
", @"
#version 330 core
out vec4 FragColor;

void main()
{
    FragColor = vec4(0.0f, 1.0f, 0.0f, 1.0f);
}
");

    public override void Load()
    {
        Shader.Generate();
        Shader.Link();

        foreach (var component in Components)
        {
            component.Generate();
        }
    }

    public override void Update(float delta)
    {
        foreach (var component in Components)
        {
            component.Update();
        }
    }

    public override void Render(CameraComponent camera)
    {
        Shader.Use();
        Shader.SetUniform("view", camera.ViewMatrix);
        Shader.SetUniform("projection", camera.ProjectionMatrix);

        foreach (var component in Components)
        {
            Shader.SetUniform("model", component.Actor.Transform.WorldMatrix);
            component.Render();
        }
    }

    public override bool Accepts(Type type) => type == ComponentType;
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;

public class CameraFrustumSystem : PrimitiveSystem<CameraFrustumComponent>
{
    public override uint Order { get; protected set; } = 100;

    public override void Load()
    {
        Shader.FragmentShaderCode = @"#version 330 core
out vec4 FragColor;

void main()
{
    FragColor = vec4(0.0f, 0.0f, 1.0f, 0.5f);
}";

        base.Load();
    }
}
