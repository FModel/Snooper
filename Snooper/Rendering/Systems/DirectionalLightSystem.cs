using System.Numerics;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Light;

namespace Snooper.Rendering.Systems;

public sealed class DirectionalLightSystem : ActiveComponentSystem<DirectionalLightComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Environment;
    public override uint Order => 4;

    protected override bool CanTakeOver(DirectionalLightComponent component)
    {
        const float maxIntensity = 30.0f;
        const float minLuminance = 0.1f;

        if (!component.IsAtmosphereSun || !component.CastShadows || component.Intensity > maxIntensity) return false;
        if (Vector3.Dot(component.Color, new Vector3(0.2126f, 0.7152f, 0.0722f)) < minLuminance) return false;

        return component.GetDirection().Y > 0.0f;
    }
}
