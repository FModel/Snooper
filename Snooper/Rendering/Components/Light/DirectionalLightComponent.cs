using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Components.Light;

public class DirectionalLightComponent : LightComponent
{
    public DirectionalLightComponent(UDirectionalLightComponent component) : base(component)
    {
        // forward axis difference, don't ask why only here
        LocalTransform.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    }

    public DirectionalLightComponent(float intensity, Vector3 color, Transform? transform = null, string? name = null) : base(intensity, color, transform, name)
    {
        // manually placed directional lights should be at the origin, just for easy manipulation
        LocalTransform.Position = Vector3.Zero;
    }

    protected override DebugComponent CreateDebugVisualization() => new ArrowComponent(Settings.DirectionalLight, null, $"{Name} (Direction)");

    internal override string Icon => "\uf185";
}
