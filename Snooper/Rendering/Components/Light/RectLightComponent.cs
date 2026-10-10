using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Systems;
using System.Numerics;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

public class RectLightComponent : LocalLightComponent
{
    public readonly float Width;
    public readonly float Height;
    public readonly float BarnDoorAngle;
    public readonly float BarnDoorLength;
    public readonly float LightFunctionConeAngle;

    public RectLightComponent(URectLightComponent component) : base(component, "S_LightRect")
    {
        Width = component.SourceWidth * Settings.GlobalScale;
        Height = component.SourceHeight * Settings.GlobalScale;
        BarnDoorAngle = component.BarnDoorAngle;
        BarnDoorLength = component.BarnDoorLength * Settings.GlobalScale;
        LightFunctionConeAngle = component.LightFunctionConeAngle;
    }

    public RectLightComponent(float intensity, Vector3? color = null, float width = 0.64f, float height = 0.64f, float attenuationRadius = 10.0f, Transform? transform = null, string? name = null) : base(attenuationRadius, intensity, color ?? Vector3.One, true, "S_LightRect", transform, name)
    {
        Width = width;
        Height = height;
        BarnDoorAngle = 88.0f;
        BarnDoorLength = 0.2f;
    }

    protected override DebugComponent CreateDebugVisualization() => new RectLightComponentVisualization(this);

    protected override bool DrawLightControls()
    {
        var edited = base.DrawLightControls();

        EditorUI.Text("Source", $"Width: {Width:F2}, Height: {Height:F2}");
        EditorUI.Text("Barn Doors", $"Angle: {BarnDoorAngle:F1} deg, Length: {BarnDoorLength:F2}");
        if (LightFunctionConeAngle > 0.0f) EditorUI.Text("Light Function Cone", $"{LightFunctionConeAngle:F1} deg");

        return edited;
    }

    protected override void SetLightData(ref LightData lightData)
    {
        base.SetLightData(ref lightData);

        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);

        lightData.Type = 2;
        lightData.Direction = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));
        lightData.SizeX = Width;
        lightData.SizeY = Height;
        lightData.Tangent = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));
    }
}
