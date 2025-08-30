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
        
        foreach (var component in _roots)
        {
            UpdateTransformComponentsRecursive(component);
        }
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        
        foreach (var component in _roots)
        {
            UpdateTransformComponentsRecursive(component);
        }
    }

    public override void Render(CameraComponent camera)
    {

    }

    protected override void OnActorComponentAdded(SpatialComponent component)
    {
        base.OnActorComponentAdded(component);

        if (component.Relation is null)
        {
            _roots.Add(component);
        }
    }

    protected override void OnActorComponentRemoved(SpatialComponent component)
    {
        base.OnActorComponentRemoved(component);

        _roots.Remove(component);
    }

    private static void UpdateTransformComponentsRecursive(SpatialComponent component)
    {
        component.UpdateLocalMatrix();
        component.UpdateWorldMatrixInternal(false);
        
        // foreach (var child in component.Actor.Children)
        // {
        //     UpdateTransformComponentsRecursive(child);
        // }
    }

    private readonly HashSet<SpatialComponent> _roots = [];
}
