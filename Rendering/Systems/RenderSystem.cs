using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Culling;

namespace Snooper.Rendering.Systems;

public class RenderSystem : PrimitiveSystem<CullingComponent>
{
    public override uint Order { get => 21; }
    protected override bool AllowDerivation { get => true; }

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
            // TODO: do this OnUpdateFrame instead
            if (camera.FrustumCullingEnabled)
            {
                component.Update(camera);
            }

            Shader.SetUniform("color", component.DebugColor);
            Shader.SetUniform("model", component.Actor.Transform.WorldMatrix);
            component.Render();
        }
    }

    protected override void OnActorComponentAdded(CullingComponent component)
    {
        base.OnActorComponentAdded(component);

        if (component is BoxCullingComponent box && _debugComponents.TryAdd(component, new DebugComponent(box)))
        {
            component.Actor?.Components.Add(_debugComponents[component]);
        }
    }

    protected override void OnActorComponentRemoved(CullingComponent component)
    {
        base.OnActorComponentRemoved(component);

        if (_debugComponents.Remove(component, out var debugComponent))
        {
            component.Actor?.Components.Remove(debugComponent);
        }
    }

    private readonly Dictionary<CullingComponent, DebugComponent> _debugComponents = [];
}
