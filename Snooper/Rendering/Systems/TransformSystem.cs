using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<SpatialComponent>
{
    public override uint Order => 9;

    protected override void OnLoad()
    {
        base.OnLoad();
        
        Parallel.ForEach(Components, UpdateTransformComponentsRecursive);
    }

    protected override void OnUpdate(float delta)
    {
        base.OnUpdate(delta);

        Parallel.ForEach(Components, UpdateTransformComponentsRecursive);
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
        component.UpdateWorldMatrix(false);
        
        foreach (var child in component.Children)
        {
            UpdateTransformComponentsRecursive(child);
        }
    }
}
