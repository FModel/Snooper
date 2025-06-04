using System.Numerics;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent> : ActorSystem<TComponent> where TComponent : TPrimitiveComponent<TVertex> where TVertex : unmanaged
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;

    protected ShaderProgram Shader { get; } = new(
"""
#version 330 core
layout (location = 0) in vec3 aPos;

uniform mat4 uModelMatrix;
uniform mat4 uViewProjectionMatrix;

void main()
{
    gl_Position = uViewProjectionMatrix * uModelMatrix * vec4(aPos, 1.0);
}
""", """
#version 330 core

out vec4 FragColor;

void main()
{
   FragColor = vec4(0.0f, 0.0f, 1.0f, 1.0f);
}
""");

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
        Shader.SetUniform("uViewProjectionMatrix", camera.ViewProjectionMatrix);

        RenderComponents(Shader);
    }

    protected void RenderComponents(ShaderProgram shader)
    {
        foreach (var component in Components.Where(component => component.Actor is not null && component.Actor.IsVisible))
        {
            shader.SetUniform("uModelMatrix", component.GetModelMatrix());
            component.Render();
        }
    }
}

public class PrimitiveSystem : PrimitiveSystem<Vector3, PrimitiveComponent>;
