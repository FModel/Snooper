using Snooper.Core.Systems;
using Snooper.Rendering.Components.Skybox;

namespace Snooper.Rendering.Systems;

public sealed class FogSystem : ActiveComponentSystem<ExponentialHeightFogComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Environment;
    public override uint Order => 2;

    protected override bool CanTakeOver(ExponentialHeightFogComponent component)
    {
        return component is { Density: > 0.0f, MaxOpacity: > 0.0f };
    }
}
