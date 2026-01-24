using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Light;

public class PointLightComponent : LocalLightComponent
{
    public readonly float LightFalloffExponent;
    public readonly float SourceRadius;
    public readonly float SoftSourceRadius;
    public readonly float SourceLength;

    public PointLightComponent(UPointLightComponent component) : base(component)
    {
        LightFalloffExponent = component.LightFalloffExponent;
        SourceRadius = component.SourceRadius * Settings.GlobalScale;
        SoftSourceRadius = component.SoftSourceRadius * Settings.GlobalScale;
        SourceLength = component.SourceLength * Settings.GlobalScale;
    }

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        lightData.Type = 0;
    }
}
