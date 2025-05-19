using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;

namespace Snooper.Rendering.Systems;

public class PrimitiveSystem : ActorSystem<PrimitiveComponent>
{
    private readonly ShaderProgram _shader = new(@"
#version 330 core
layout (location = 0) in vec3 aPos;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = projection * view * model * vec4(aPos.x, aPos.y, aPos.z, 1.0);
}
", @"
#version 330 core
out vec4 FragColor;

void main()
{
    FragColor = vec4(1.0f, 0.5f, 0.2f, 1.0f);
}
");

    public override void Load()
    {
        _shader.Generate();
        _shader.Link();

        foreach (var component in Components)
        {
            component.Generate();
        }
    }

    public override void Update(float delta)
    {

    }

    public override void Render()
    {
        if (ActorManager is SceneSystem { CurrentCamera: { } camera })
        {
            _shader.Use();
            _shader.SetUniform("view", camera.ViewMatrix);
            _shader.SetUniform("projection", camera.ProjectionMatrix);
        }

        foreach (var component in Components)
        {
            _shader.SetUniform("model", component.Actor.Transform.WorldMatrix);
            component.Render();
        }
    }
}
