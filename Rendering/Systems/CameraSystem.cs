using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public sealed class CameraSystem : ActorSystem<CameraComponent>
{
    public override uint Order { get; protected set; } = 10;

    public override void Load()
    {

    }

    public override void Update(float delta)
    {
        foreach (var cameraComponent in Components)
        {
            cameraComponent.Update();
        }
    }

    public override void Render(CameraComponent camera)
    {

    }
}
