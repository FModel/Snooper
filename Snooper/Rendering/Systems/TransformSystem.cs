using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<SpatialComponent>
{
    public override uint Order => 9;

    /// <summary>
    /// useful to update shadows
    /// </summary>
    internal static event Action<SpatialComponent>? OnTransformComponentUpdated;

    protected override void OnComponentUpdate(SpatialComponent component, float delta)
    {
        OnTransformComponentUpdated?.Invoke(component);
        UpdateTransformComponentsRecursive(component);
    }

    protected override void OnRender(CameraComponent camera)
    {

    }

    protected override bool CanEnqueueActorComponent(SpatialComponent component)
    {
        return component.Relation is null;
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
