using Snooper.Core.Systems;
using Snooper.Rendering.Components.Light;

namespace Snooper.Rendering.Systems;

public sealed class SkyLightSystem : ActiveComponentSystem<SkyLightComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Environment;
    public override uint Order => 3;
}
