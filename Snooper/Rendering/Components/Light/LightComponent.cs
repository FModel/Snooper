using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Light;

[DefaultActorSystem(typeof(LightSystem))]
public class LightComponent : SpatialComponent
{
    public readonly float Intensity;
    public readonly Vector3 Color;

    internal BufferAllocation? _lightDataAllocation;

    public LightComponent(ULightComponentBase component) : base(component)
    {
        // Intensity = component.Intensity;
        Intensity = MathF.PI; // because games use weird values sometimes, for consistency the intensity will never change
        Color = component.GetLightColor();
    }

    public LightComponent(float intensity, Vector3 color, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Intensity = intensity;
        Color = color;
    }

    private LightData? _cachedLightData;
    public LightData GetLightData()
    {
        if (_cachedLightData is null)
        {
            var data = new LightData();
            SetLightData(ref data);
            _cachedLightData = data;
        }

        return _cachedLightData.Value;
    }

    protected virtual void SetLightData(ref LightData lightData)
    {
        lightData.Position = WorldMatrix.Translation;
        lightData.Color = Color;
        lightData.Intensity = Intensity;
    }

    internal override string Icon => "bulb";
}
