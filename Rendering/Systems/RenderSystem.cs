using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Culling;

namespace Snooper.Rendering.Systems;

public class RenderSystem : PrimitiveSystem<CullingComponent>
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

    public override void Render(CameraComponent camera)
    {
        if (camera.IsActive)
        {
            // TODO: do this OnUpdateFrame instead
            foreach (var component in Components)
            {
                component.Update(camera);
            }
        }

        base.Render(camera);
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
