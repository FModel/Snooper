using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public sealed class CameraSystem : ActiveComponentSystem<CameraComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Scene;
    public override uint Order => 10;

    protected override bool CanTakeOver(CameraComponent component)
    {
        return false;
    }

    protected override void OnComponentUpdate(CameraComponent component, float delta)
    {
        component.UpdateMatrices();
    }
}
