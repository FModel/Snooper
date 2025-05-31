using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public sealed class CameraSystem : ActorSystem<CameraComponent>
{
    public override uint Order { get => 10; }

    public override void Load()
    {

    }

    public override void Update(float delta)
    {
        foreach (var component in Components)
        {
            component.Update();
        }
    }

    public override void Render(CameraComponent camera)
    {

    }
}
