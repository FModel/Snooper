using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Culling;

namespace Snooper.Rendering.Systems;

public class CullingSystem : ActorSystem<CullingComponent>
{
    public override uint Order { get => 11; }
    protected override bool AllowDerivation { get => true; }

    public override void Load()
    {

    }

    public override void Update(float delta)
    {

    }

    public override void Render(CameraComponent camera)
    {
        if (!camera.IsActive) return;

        // TODO: do this OnUpdateFrame instead
        foreach (var component in Components.OfType<BoxCullingComponent>())
        {
            component.Update(camera);
        }
    }

    protected override void OnActorComponentAdded(CullingComponent component)
    {
        base.OnActorComponentAdded(component);

        var added = component switch
        {
            BoxCullingComponent box => _debugComponents.TryAdd(component, new DebugComponent(box)),
            SphereCullingComponent sphere => _debugComponents.TryAdd(component, new DebugComponent(sphere)),
            _ => false
        };

        if (added) component.Actor?.Components.Add(_debugComponents[component]);
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
