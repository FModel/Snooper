using CUE4Parse.UE4.Assets.Exports.Component.Lights;

namespace Snooper.Rendering.Components.Light;

public class SkyLightComponent : LightComponent
{
    public SkyLightComponent(USkyLightComponent component) : base(component)
    {

    }

    internal override string Icon => "sun";
}
