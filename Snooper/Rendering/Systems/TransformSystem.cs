using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<SpatialComponent>
{
    public override uint Order => 9;

    public override void Load()
    {
        base.Load();
        
        Parallel.ForEach(Components, UpdateTransformComponentsRecursive);
    }

    public override void Update(float delta)
    {
        base.Update(delta);

        Parallel.ForEach(Components, UpdateTransformComponentsRecursive);
    }

    public override void Render(CameraComponent camera)
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
