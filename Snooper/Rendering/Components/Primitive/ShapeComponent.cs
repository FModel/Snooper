using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Components.Primitive;

public abstract class ShapeComponent : DebugComponent
{
    protected Vector3? Color;
    protected readonly float LineThickness = 2.0f;

    protected ShapeComponent(UShapeComponent component) : base(component)
    {
        if (component.TryGetValue(out FColor color, "ShapeColor"))
        {
            Color = new Vector3(color.R, color.G, color.B) / 255f;
        }

        // LineThickness = component.GetOrDefault("LineThickness", LineThickness);
    }

    protected ShapeComponent(Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : base(color, lineThickness, transform, name)
    {

    }

    public override string Icon => "\uf61f";
}
