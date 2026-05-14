using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Light;

public class SpotLightComponent : PointLightComponent
{
    public readonly float InnerConeAngle;
    public readonly float OuterConeAngle;

    public SpotLightComponent(USpotLightComponent component) : base(component, "S_LightSpot")
    {
        InnerConeAngle = component.InnerConeAngle;
        OuterConeAngle = component.OuterConeAngle;
    }

    protected override DebugComponent CreateDebugVisualization() => new SpotLightComponentVisualization(this);

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);

        lightData.Type = 1;
        lightData.Direction = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        lightData.SpotAngle = MathF.Cos(InnerConeAngle * MathF.PI / 180.0f);
        lightData.SpotOuterAngle = MathF.Cos(OuterConeAngle * MathF.PI / 180.0f);
    }
}
