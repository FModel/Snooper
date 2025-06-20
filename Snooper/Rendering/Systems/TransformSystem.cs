using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<TransformComponent>
{
    public override uint Order => 9;

    public override void Load()
    {
        base.Load();
        foreach (var component in _transformRoots)
        {
            UpdateTransformComponentsRecursive(component);
        }
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        foreach (var component in _transformRoots)
        {
            UpdateTransformComponentsRecursive(component);
        }
    }

    public override void Render(CameraComponent camera)
    {

    }

    protected override void OnActorComponentAdded(TransformComponent component)
    {
        base.OnActorComponentAdded(component);

        if (component.Relation is null)
        {
            _transformRoots.Add(component);
        }
    }

    protected override void OnActorComponentRemoved(TransformComponent component)
    {
        base.OnActorComponentRemoved(component);

        _transformRoots.Remove(component);
    }

    private static void UpdateTransformComponentsRecursive(TransformComponent transformComponent)
    {
        transformComponent.UpdateLocalMatrix();
        transformComponent.UpdateWorldMatrixInternal(false);

        foreach (var child in transformComponent.Children)
        {
            UpdateTransformComponentsRecursive(child);
        }
    }

    private readonly HashSet<TransformComponent> _transformRoots = [];
}
