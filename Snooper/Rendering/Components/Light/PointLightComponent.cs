using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Light;

public class PointLightComponent : LocalLightComponent
{
    public readonly float LightFalloffExponent;
    public readonly float SourceRadius;
    public readonly float SoftSourceRadius;
    public readonly float SourceLength;
    public readonly bool UseInverseSquaredFalloff;

    public PointLightComponent(UPointLightComponent component, string sprite = "S_LightPoint") : base(component, sprite)
    {
        LightFalloffExponent = component.LightFalloffExponent;
        SourceRadius = component.SourceRadius * Settings.GlobalScale;
        SoftSourceRadius = component.SoftSourceRadius * Settings.GlobalScale;
        SourceLength = component.SourceLength * Settings.GlobalScale;
        UseInverseSquaredFalloff = component.bUseInverseSquaredFalloff;
    }

    protected override DebugComponent CreateDebugVisualization() => new PointLightComponentVisualization(this);

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        lightData.Type = 0;
        lightData.UseInverseSquaredFalloff = UseInverseSquaredFalloff ? 1u : 0u;
    }
}
