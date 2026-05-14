using CUE4Parse.UE4.Assets.Exports.Component.Lights;
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
