using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Core;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

[DefaultActorSystem(typeof(DirectionalLightSystem))]
public class DirectionalLightComponent : LightComponent
{
    public readonly float SourceAngle;
    public readonly bool IsAtmosphereSun;

    // because we don't auto exposure, the intensity a sun comes with is a full day whatever the number, lowering it goes to night
    public float Daylight => field > 0.0f ? Math.Clamp(Intensity / field, 0.0f, 1.0f) : 0.0f;

    public DirectionalLightComponent(UDirectionalLightComponent component) : base(component, "S_LightDirectional")
    {
        Daylight = Intensity;
        SourceAngle = component.LightSourceAngle;
        IsAtmosphereSun = component is { bAtmosphereSunLight: true, AtmosphereSunLightIndex: 0 };

        // forward axis difference, don't ask why only here (same for CameraComponent)
        LocalTransform.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    }

    public DirectionalLightComponent(float intensity, Vector3 color, Transform? transform = null, string? name = null) : base(intensity, color, true, "S_LightDirectional", transform, name)
    {
        Daylight = Intensity;
        SourceAngle = 0.5357f;
        IsAtmosphereSun = true;

        // manually placed directional lights should be at the origin, just for easy manipulation
        // LocalTransform.Position = Vector3.Zero;
    }

    public Vector3 GetDirection()
    {
        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);
        return Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));
    }

    protected override DebugComponent CreateDebugVisualization() => new ArrowComponent(Settings.DirectionalLight, null, $"{Name} (Direction)");

    public override string Icon => Settings.SunIcon;

    protected override bool DrawLightControls()
    {
        EditorUI.Text("Source Angle", $"{SourceAngle}°");
        EditorUI.Text("Atmosphere Sun", IsAtmosphereSun ? "Yes" : "No");

        return base.DrawLightControls();
    }
}
