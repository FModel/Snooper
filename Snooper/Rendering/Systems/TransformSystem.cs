using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<SpatialComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Custom;
    public override uint Order => 9;

    protected override void OnComponentUpdate(SpatialComponent component, float delta)
    {
        UpdateTransformComponentsRecursive(component);
    }

    protected override void OnRender(CameraComponent camera, CommandBufferType type)
    {

    }

    private void UpdateTransformComponentsRecursive(SpatialComponent component)
    {
        if (!component.IsDirty(DirtyFlags.Transform)) return;

        component.UpdateWorldMatrix(false);

        foreach (var child in component.Children)
        {
            UpdateTransformComponentsRecursive(child);
        }
    }
}
