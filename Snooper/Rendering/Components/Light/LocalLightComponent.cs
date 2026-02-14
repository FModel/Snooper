using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

public abstract class LocalLightComponent : LightComponent
{
    public readonly float AttenuationRadius;

    public LocalLightComponent(ULocalLightComponent component) : base(component)
    {
        AttenuationRadius = component.AttenuationRadius * Settings.GlobalScale;
    }

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        lightData.Type = uint.MaxValue; // to override in child classes
        lightData.Range = AttenuationRadius;
    }

    protected override void DrawLightControls()
    {
        base.DrawLightControls();

        EditorUI.Text("Attenuation Radius", $"{AttenuationRadius:F}");
    }
}
