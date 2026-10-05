using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

public abstract class LightComponent : LightComponentBase
{
    public ELightUnits IntensityUnits;
    public readonly float MaxDrawDistance;
    public readonly float MaxDistanceFadeRange;

    protected LightComponent(ULightComponent component, string sprite) : base(component, sprite)
    {
        IntensityUnits = component.GetLightUnits();

        if (component.bUseTemperature)
        {
            Color *= FLinearColor.MakeFromColorTemperature(component.Temperature);
        }

        MaxDrawDistance = component.MaxDrawDistance * Settings.GlobalScale;
        MaxDistanceFadeRange = component.MaxDistanceFadeRange * Settings.GlobalScale;
    }

    protected LightComponent(float intensity, Vector3 color, bool castShadows, string sprite, Transform? transform = null, string? name = null) : base(intensity, color, castShadows, sprite, transform, name)
    {

    }

    public override string Icon => "\uf0eb";

    protected override bool DrawLightControls()
    {
        if (MaxDrawDistance > 0.0f)
        {
            EditorUI.Text("Draw Distance", $"Min: 0, Max: {MaxDrawDistance}, Fade: {MaxDistanceFadeRange}");
        }
        EditorUI.Text("Intensity Units", IntensityUnits.ToString());

        return base.DrawLightControls();
    }
}
