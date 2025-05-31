using System.Numerics;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class RenderSystem : PrimitiveSystem<StaticMeshComponent>
{
    protected override ShaderProgram Shader { get; } = new(@"
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

uniform vec3 color;

out vec4 FragColor;

void main()
{
    FragColor = vec4(color, 1.0f);
}
");

    public override void Render(CameraComponent camera)
    {
        Shader.Use();
        Shader.SetUniform("view", camera.ViewMatrix);
        Shader.SetUniform("projection", camera.ProjectionMatrix);

        foreach (var component in Components)
        {
            if (camera.Actor is CameraActor)
            {
                var color = new Vector3(0.0f, 1.0f, 0.0f);
                if (!component.IsInFrustum(camera))
                {
                    color = new Vector3(1.0f, 0.0f, 0.0f);
                }
                Shader.SetUniform("color", color);
            }

            Shader.SetUniform("model", component.Actor.Transform.WorldMatrix);
            component.Render();
        }
    }
}
