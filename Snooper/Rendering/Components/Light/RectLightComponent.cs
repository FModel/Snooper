using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Systems;
using System.Numerics;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Components.Light;

public class RectLightComponent : LocalLightComponent
{
    public readonly float Width;
    public readonly float Height;
    public readonly float BarnDoorAngle;
    public readonly float BarnDoorLength;
    public readonly float LightFunctionConeAngle;

    public RectLightComponent(URectLightComponent component) : base(component)
    {
        Width = component.SourceWidth * Settings.GlobalScale;
        Height = component.SourceHeight * Settings.GlobalScale;
        BarnDoorAngle = component.BarnDoorAngle;
        BarnDoorLength = component.BarnDoorLength * Settings.GlobalScale;
        LightFunctionConeAngle = component.LightFunctionConeAngle;
    }

    protected override DebugComponent CreateDebugVisualization() => new RectLightComponentVisualization(this);

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);

        lightData.Type = 2;
        lightData.Direction = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        lightData.SizeX = Width;
        lightData.SizeY = Height;
        lightData.UpVector = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));
    }
}
