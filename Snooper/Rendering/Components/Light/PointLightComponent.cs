using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Light;

public class PointLightComponent : LightComponent
{
    public readonly float AttenuationRadius;

    public PointLightComponent(UPointLightComponent component) : base(component)
    {
        AttenuationRadius = component.AttenuationRadius * Settings.GlobalScale;
    }

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        lightData.Type = 0;
        lightData.Range = AttenuationRadius;
    }
}
