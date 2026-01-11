using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Light;

public class DirectionalLightComponent : LightComponent
{
    public DirectionalLightComponent(UDirectionalLightComponent component) : base(component)
    {

    }

    public DirectionalLightComponent(float intensity, Vector3 color, Transform? transform = null, string? name = null) : base(intensity, color, transform, name)
    {
        // manually placed directional lights should be at the origin, just for easy manipulation
        LocalTransform.Position = Vector3.Zero;
    }
}
