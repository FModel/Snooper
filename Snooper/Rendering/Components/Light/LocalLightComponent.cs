using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Objects.Engine;
using ImGuiNET;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

public abstract class LocalLightComponent : LightComponent
{
    public float AttenuationRadius;

    public LocalLightComponent(ULocalLightComponent component, string sprite) : base(component, sprite)
    {
        AttenuationRadius = component.AttenuationRadius * Settings.GlobalScale;

        // the shader wants candela from every local light
        float? cosHalfConeAngle = component switch
        {
            UPointLightComponent { bUseInverseSquaredFalloff: false } => null,
            USpotLightComponent spot => spot.GetCosHalfConeAngle(),
            UPointLightComponent => -1.0f,
            _ => 0.0f
        };

        if (cosHalfConeAngle is { } cosine)
        {
            Intensity = IntensityUnits == ELightUnits.EV
                ? LightUtils.EV100ToLuminance(Intensity)
                : Intensity * LightUtils.GetUnitsConversionFactor(IntensityUnits, ELightUnits.Candelas, cosine);
            IntensityUnits = ELightUnits.Candelas;
        }
    }

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        lightData.Type = uint.MaxValue; // to override in child classes
        lightData.Range = AttenuationRadius;
    }

    protected override bool DrawLightControls()
    {
        base.DrawLightControls();

        EditorUI.Property("Attenuation Radius");
        return ImGui.DragFloat("##AttenuationRadius", ref AttenuationRadius, 0.1f, 0f, float.MaxValue, "%.1f");
    }
}
