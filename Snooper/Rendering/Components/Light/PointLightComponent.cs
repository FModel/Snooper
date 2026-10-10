using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Systems;
using Snooper.UI;

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

    public PointLightComponent(float intensity, Vector3? color = null, float attenuationRadius = 10.0f, float sourceRadius = 0.0f, Transform? transform = null, string? name = null) : this(attenuationRadius, intensity, color ?? Vector3.One, true, "S_LightPoint", transform, name)
    {
        SourceRadius = sourceRadius;
    }

    protected PointLightComponent(float attenuationRadius, float intensity, Vector3 color, bool castShadows, string sprite, Transform? transform = null, string? name = null) : base(attenuationRadius, intensity, color, castShadows, sprite, transform, name)
    {
        LightFalloffExponent = 8.0f;
        UseInverseSquaredFalloff = true;
    }

    protected override DebugComponent CreateDebugVisualization() => new PointLightComponentVisualization(this);

    protected override bool DrawLightControls()
    {
        var edited = base.DrawLightControls();

        EditorUI.Text("Source", $"Radius: {SourceRadius:F2}, Soft Radius: {SoftSourceRadius:F2}, Length: {SourceLength:F2}");
        EditorUI.Text("Falloff", UseInverseSquaredFalloff ? "Inverse Squared" : $"Exponent: {LightFalloffExponent:F1}");

        return edited;
    }

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);

        lightData.Type = 0;
        lightData.UseInverseSquaredFalloff = UseInverseSquaredFalloff;
        lightData.FalloffExponent = LightFalloffExponent;
        lightData.SourceRadius = SourceRadius;
        lightData.SourceLength = SourceLength;
        lightData.Tangent = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));
    }
}
