using Snooper.Core.Systems;
using Snooper.Rendering.Components;

namespace Snooper.Rendering.Systems;

public sealed class TransformSystem : ActorSystem<TransformComponent>
{
    public override void Load()
    {

    }

    public override void Update(float delta)
    {
        foreach (var transformComponent in Components)
        {
            UpdateTransformComponentsRecursive(transformComponent);
        }
    }

    public override void Render()
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
