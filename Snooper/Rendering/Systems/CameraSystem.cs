using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public sealed class CameraSystem : ActorSystem<CameraComponent>
{
    public override uint Order => 10;

    protected override void OnComponentUpdate(CameraComponent component, float delta)
    {
        component.Update();
    }

    protected override void OnRender(CameraComponent camera)
    {

    }
}
