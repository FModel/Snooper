using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Systems;
using Snooper.UI;

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

    public SpotLightComponent(float intensity, Vector3? color = null, float outerConeAngle = 44.0f, float innerConeAngle = 0.0f, float attenuationRadius = 10.0f, Transform? transform = null, string? name = null) : base(attenuationRadius, intensity, color ?? Vector3.One, true, "S_LightSpot", transform, name)
    {
        InnerConeAngle = innerConeAngle;
        OuterConeAngle = outerConeAngle;
    }

    protected override DebugComponent CreateDebugVisualization() => new SpotLightComponentVisualization(this);

    protected override bool DrawLightControls()
    {
        var edited = base.DrawLightControls();

        EditorUI.Text("Cone", $"Inner: {InnerConeAngle:F1} deg, Outer: {OuterConeAngle:F1} deg");

        return edited;
    }

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
