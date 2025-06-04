using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<TransformComponent>
{
    public override uint Order => 9;

    public override void Update(float delta)
    {
        base.Update(delta);
        foreach (var component in Components)
        {
            UpdateTransformComponentsRecursive(component);
        }
    }

    public override void Render(CameraComponent camera)
    {

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
}
